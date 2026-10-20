using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    [NodeDescription("Picks the most important unmet goal, plans a chain of its GOAP Action children to reach it, and runs the plan.",
        "Goals: each is a list of conditions on blackboard keys, with a priority (fixed or read from a key). The planner pursues the highest-priority goal that is not met and can be planned.\n" +
        "While a plan runs: every 'Goal Check Interval' seconds a more important goal can take over; before each action its preconditions are checked on the live blackboard; " +
        "a failed action, a broken precondition or a plan that ends without reaching the goal triggers a replan (up to 'Max Replans').\n" +
        "Succeeds when the goal is reached (or every goal already holds); fails when no plan exists or the replan budget is used up.")]
    [NodeInfo("GOAP Planner", "GOAP/Planner", true, true, iconPath: "Assets/ND_BehaviorTree/NDBT/Icons/Brain.png")]
    public class GOAPPlannerNode : CompositeNode, IOrderIndependentChildren
    {
        public List<GoapGoal> goals = new List<GoapGoal>();
        [Tooltip("Seconds between checks for a more important goal while a plan runs (0 = only when a plan ends).")]
        public float goalCheckInterval = 0.5f;
        [Tooltip("Replans allowed per activation (failed action, broken precondition, plan ended without reaching the goal).")]
        public int maxReplans = 3;
        [Tooltip("Search budget per plan (world states expanded).")]
        public int maxIterations = 2000;
        [Tooltip("Log each new plan to the Console.")]
        public bool logPlans;

        private const string LogTag = "[GOAPPlannerNode]";

        // ---- compiled domain (built once per runtime clone) ----
        [NonSerialized] private bool m_Compiled;
        [NonSerialized] private string m_CompileError;
        [NonSerialized] private List<Key> m_FactKeys;
        [NonSerialized] private Dictionary<string, int> m_FactIndex;
        [NonSerialized] private List<Node> m_Entries;            // child entry (outermost decorator or the action)
        [NonSerialized] private List<GOAPActionNode> m_Actions;  // the action behind each entry
        [NonSerialized] private List<GoapPlanAction> m_PlanActions;
        [NonSerialized] private Key[] m_CostKeys;
        [NonSerialized] private GoapFactCondition[][] m_GoalConditions;
        [NonSerialized] private Key[] m_PriorityKeys;

        // ---- activation state ----
        [NonSerialized] private int m_Goal = -1;
        [NonSerialized] private List<int> m_Plan;
        [NonSerialized] private int m_Step;
        [NonSerialized] private bool m_StepStarted;
        [NonSerialized] private bool m_AllGoalsMet;
        [NonSerialized] private int m_Replans;
        [NonSerialized] private float m_NextGoalCheck;
        [NonSerialized] private bool m_WarnedThisActivation;
        [NonSerialized] private bool m_GoalCheckRequested;

        /// <summary>The goal being pursued (null when idle or every goal holds).</summary>
        public GoapGoal CurrentGoal => m_Goal >= 0 && m_Goal < goals.Count ? goals[m_Goal] : null;
        /// <summary>The actions of the current plan, in order (empty when there is none).</summary>
        public IReadOnlyList<GOAPActionNode> CurrentPlan => m_Plan == null ? (IReadOnlyList<GOAPActionNode>)Array.Empty<GOAPActionNode>() : m_Plan.Select(i => m_Actions[i]).ToList();
        public int CurrentStep => m_Step;
        /// <summary>Why the last plan attempt failed, else null.</summary>
        public string LastFailure { get; private set; }

        /// <summary>
        /// Re-evaluate goals on the next tick (a more important goal may take over the running plan), e.g. when a
        /// sensor or gameplay event changed something important. Works even with Goal Check Interval = 0.
        /// </summary>
        public void RequestGoalCheck() => m_GoalCheckRequested = true;

        /// <summary>The title shown on the graph (typeName), else the asset name.</summary>
        public static string Title(Node node) => node == null ? "(none)" : string.IsNullOrEmpty(node.typeName) ? node.name : node.typeName;

        private enum PlanResult { Planned, AllGoalsMet, Failed }

        /// <summary>
        /// Checks goals and actions against <paramref name="source"/> (e.g. the tree's blackboard asset in the editor).
        /// Returns why the planner would fail to start, or null. Does not log; does not touch a running planner's state
        /// beyond its cached domain, so call it on the asset node, not on a runtime clone.
        /// </summary>
        public string CheckConfiguration(Blackboard source)
        {
            Compile(source, log: false);
            string error = m_CompileError;
            m_Compiled = false; // the runtime clone compiles against its own blackboard
            return error;
        }

        /// <summary>
        /// Plans from the current values of <paramref name="source"/> (editor preview / debugging) without running anything.
        /// Returns a one-line description: the chosen goal and plan, "every goal already holds", or why it failed.
        /// </summary>
        public string PreviewPlan(Blackboard source)
        {
            Compile(source, log: false);
            try
            {
                if (m_CompileError != null) return $"Misconfigured: {m_CompileError}";
                var state = ReadState();
                if (TryPlan(state, float.NegativeInfinity, out int goal, out var plan, out string reason))
                    return $"Goal '{goals[goal].name}' (priority {Priority(goal)}): {(plan.Count == 0 ? "already met" : string.Join(" → ", plan.Select(i => Title(m_Actions[i]))))}";
                return reason == null ? "Every goal already holds: nothing to do." : $"No plan: {reason}";
            }
            finally { m_Compiled = false; }
        }

        protected override void OnEnter()
        {
            base.OnEnter();
            m_Replans = 0;
            m_WarnedThisActivation = false;
            m_NextGoalCheck = Time.time + goalCheckInterval;
            if (!m_Compiled) Compile(blackboard, log: true);
            if (m_CompileError != null) return;
            Replan();
        }

        protected override Status OnProcess()
        {
            if (m_CompileError != null) return Status.Failure;
            if (m_AllGoalsMet) return Status.Success;
            if (m_Plan == null) return Status.Failure;

            if (m_GoalCheckRequested || (goalCheckInterval > 0f && Time.time >= m_NextGoalCheck))
            {
                m_GoalCheckRequested = false;
                m_NextGoalCheck = Time.time + goalCheckInterval;
                TrySwitchToMoreImportantGoal();
            }

            while (true)
            {
                if (m_Step >= m_Plan.Count)
                {
                    if (GoapPlanner.AllMet(ReadState(), m_GoalConditions[m_Goal])) return Status.Success;
                    return Retry($"the plan for '{goals[m_Goal].name}' finished but the goal does not hold");
                }

                int action = m_Plan[m_Step];
                if (!m_StepStarted)
                {
                    if (!GoapPlanner.AllMet(ReadState(), m_PlanActions[action].Preconditions))
                        return Retry($"preconditions of '{Title(m_Actions[action])}' no longer hold");
                    m_StepStarted = true;
                }

                var status = m_Entries[action].Process();
                if (status == Status.Running) return Status.Running;
                if (status == Status.Failure) return Retry($"action '{Title(m_Actions[action])}' failed");

                if (m_Actions[action].applyEffects) WriteEffects(action);
                m_Step++;
                m_StepStarted = false;
                if (m_Step < m_Plan.Count) return Status.Running;
                // last action done: check the goal right away (loop)
            }
        }

        public override void Abort()
        {
            AbortCurrentStep();
            base.Abort();
        }

        public override void Reset()
        {
            base.Reset();
            m_Goal = -1;
            m_Plan = null;
            m_Step = 0;
            m_StepStarted = false;
            m_AllGoalsMet = false;
        }

        // ------------------------------------------------------------------ planning

        private Status Retry(string reason)
        {
            AbortCurrentStep();
            m_Replans++;
            if (m_Replans > maxReplans)
            {
                LastFailure = $"{reason}; gave up after {maxReplans} replan(s)";
                Warn(LastFailure);
                m_Plan = null;
                return Status.Failure;
            }
            switch (Replan())
            {
                case PlanResult.AllGoalsMet: return Status.Success;
                case PlanResult.Failed: return Status.Failure;
                default: return Status.Running;
            }
        }

        private PlanResult Replan()
        {
            m_Plan = null;
            m_Step = 0;
            m_StepStarted = false;
            m_AllGoalsMet = false;
            var state = ReadState();
            if (TryPlan(state, float.NegativeInfinity, out int goal, out var plan, out string reason))
            {
                Install(goal, plan);
                return PlanResult.Planned;
            }
            m_Goal = -1;
            if (reason == null)
            {
                m_AllGoalsMet = true;
                LastFailure = null;
                return PlanResult.AllGoalsMet;
            }
            LastFailure = reason;
            Warn($"no plan: {reason}");
            return PlanResult.Failed;
        }

        private void TrySwitchToMoreImportantGoal()
        {
            if (m_Goal < 0) return;
            if (!TryPlan(ReadState(), Priority(m_Goal), out int goal, out var plan, out _) || goal == m_Goal) return;
            AbortCurrentStep();
            Install(goal, plan);
        }

        private void Install(int goal, List<int> plan)
        {
            m_Goal = goal;
            m_Plan = plan;
            m_Step = 0;
            m_StepStarted = false;
            LastFailure = null;
            if (logPlans)
                Debug.Log($"{LogTag} '{Title(this)}' goal '{goals[goal].name}': {(plan.Count == 0 ? "(already met)" : string.Join(" → ", plan.Select(i => Title(m_Actions[i]))))}", this);
        }

        /// <summary>
        /// Plans for the highest-priority unmet goal whose priority is above <paramref name="abovePriority"/>.
        /// False with <paramref name="reason"/> null when no such goal is unmet; with a reason when none could be planned.
        /// </summary>
        private bool TryPlan(object[] state, float abovePriority, out int goal, out List<int> plan, out string reason)
        {
            goal = -1;
            plan = null;
            reason = null;
            for (int a = 0; a < m_PlanActions.Count; a++) m_PlanActions[a].Cost = Cost(a);

            var order = Enumerable.Range(0, goals.Count).Select(g => (g, p: Priority(g))).Where(x => x.p > abovePriority)
                .OrderByDescending(x => x.p).ThenBy(x => x.g);
            List<string> failures = null;
            foreach (var (g, _) in order)
            {
                if (GoapPlanner.AllMet(state, m_GoalConditions[g])) continue;
                var found = GoapPlanner.FindPlan(state, m_GoalConditions[g], m_PlanActions, maxIterations, out string failure);
                if (found != null)
                {
                    goal = g;
                    plan = found;
                    return true;
                }
                (failures ??= new List<string>()).Add($"goal '{goals[g].name}': {failure}");
            }
            if (failures != null) reason = string.Join("; ", failures);
            return false;
        }

        private float Priority(int goal) => m_PriorityKeys[goal] != null ? Convert.ToSingle(m_PriorityKeys[goal].GetValueObject()) : goals[goal].priority;

        private float Cost(int action) => m_CostKeys[action] != null ? Convert.ToSingle(m_CostKeys[action].GetValueObject()) : m_Actions[action].cost;

        private object[] ReadState()
        {
            var state = new object[m_FactKeys.Count];
            for (int i = 0; i < state.Length; i++) state[i] = m_FactKeys[i].GetValueObject();
            return state;
        }

        private void WriteEffects(int action)
        {
            foreach (var effect in m_PlanActions[action].Effects)
            {
                var key = m_FactKeys[effect.Fact];
                key.SetValueObject(effect.ApplyTo(key.GetValueObject()));
            }
        }

        private void AbortCurrentStep()
        {
            if (m_Plan == null || m_Step >= m_Plan.Count || !m_StepStarted) return;
            var entry = m_Entries[m_Plan[m_Step]];
            if (entry.IsRunning()) entry.Abort();
            entry.Reset();
            m_StepStarted = false;
        }

        private void Warn(string message)
        {
            if (m_WarnedThisActivation) return;
            m_WarnedThisActivation = true;
            Debug.LogWarning($"{LogTag} '{Title(this)}': {message}. (Logged once per activation.)", this);
        }

        // ------------------------------------------------------------------ compile goals / actions into facts

        private sealed class ConfigException : Exception
        {
            public ConfigException(string message) : base(message) { }
        }

        [NonSerialized] private Blackboard m_CompileBlackboard;

        private void Compile(Blackboard source, bool log)
        {
            m_Compiled = true;
            m_CompileError = null;
            m_CompileBlackboard = source;
            try
            {
                if (source == null) throw new ConfigException("the tree has no blackboard");
                m_FactKeys = new List<Key>();
                m_FactIndex = new Dictionary<string, int>();
                m_Entries = new List<Node>();
                m_Actions = new List<GOAPActionNode>();
                m_PlanActions = new List<GoapPlanAction>();

                foreach (var entry in children)
                {
                    if (!(Terminal(entry) is GOAPActionNode action))
                        throw new ConfigException($"child '{Title(entry)}' is not a GOAP Action (only GOAP Actions go under a planner)");
                    m_Entries.Add(entry);
                    m_Actions.Add(action);
                    m_PlanActions.Add(new GoapPlanAction
                    {
                        Cost = action.cost,
                        Preconditions = action.preconditions.Select(c => Condition(c, $"action '{Title(action)}'")).ToArray(),
                        Effects = action.effects.Select(e => Effect(e, $"action '{Title(action)}'")).ToArray(),
                    });
                }
                m_CostKeys = m_Actions.Select(a => a.costKey != null ? NumberKey(a.costKey, $"cost key of '{Title(a)}'") : null).ToArray();
                m_GoalConditions = goals.Select(g => g.conditions.Select(c => Condition(c, $"goal '{g.name}'")).ToArray()).ToArray();
                m_PriorityKeys = goals.Select(g => g.priorityKey != null ? NumberKey(g.priorityKey, $"priority key of goal '{g.name}'") : null).ToArray();
                if (goals.Count == 0) throw new ConfigException("it has no goals");
            }
            catch (ConfigException e)
            {
                m_CompileError = e.Message;
                LastFailure = e.Message;
                if (log) Debug.LogError($"{LogTag} '{Title(this)}' is misconfigured: {e.Message}. It will fail until fixed.", this);
            }
        }

        private static Node Terminal(Node entry)
        {
            for (int guard = 0; guard < 64 && entry is DecoratorNode decorator && decorator.child != null; guard++) entry = decorator.child;
            return entry;
        }

        private Key RuntimeKey(Key key, string owner)
        {
            if (key == null || string.IsNullOrEmpty(key.keyName)) throw new ConfigException($"{owner} has an entry with no key");
            if (!m_CompileBlackboard.TryGetKey(key.keyName, out var runtime)) throw new ConfigException($"{owner} uses key '{key.keyName}', which is not on the blackboard");
            return runtime;
        }

        private int Fact(Key key, string owner)
        {
            var runtime = RuntimeKey(key, owner);
            if (!GoapValue.IsSupported(runtime.GetValueType()))
                throw new ConfigException($"{owner} uses key '{key.keyName}' of type {runtime.GetValueType().Name}, which GOAP cannot compare");
            if (!m_FactIndex.TryGetValue(runtime.keyName, out int index))
            {
                index = m_FactKeys.Count;
                m_FactIndex[runtime.keyName] = index;
                m_FactKeys.Add(runtime);
            }
            return index;
        }

        private GoapFactCondition Condition(KeyCondition c, string owner)
        {
            int fact = Fact(c?.key, owner);
            return new GoapFactCondition(fact, c.comparison, c.value.Get(m_FactKeys[fact].GetValueType()));
        }

        private GoapFactEffect Effect(KeyEffect e, string owner)
        {
            int fact = Fact(e?.key, owner);
            var type = m_FactKeys[fact].GetValueType();
            if (e.operation == GoapEffectOperation.Add && !IsNumber(type))
                throw new ConfigException($"{owner} adds to '{e.key.keyName}', which is not a number key");
            return new GoapFactEffect(fact, e.operation, e.value.Get(type));
        }

        private Key NumberKey(Key key, string owner)
        {
            var runtime = RuntimeKey(key, owner);
            if (!IsNumber(runtime.GetValueType())) throw new ConfigException($"{owner} '{key.keyName}' is not a number key");
            return runtime;
        }

        private static bool IsNumber(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double);
    }
}
