// FILE: GOAP/Planner.cs

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ND_BehaviorTree.GOAP
{
    public class Planner
    {
        /// <summary>
        /// Một node bên trong cây tìm kiếm của thuật toán A*.
        /// Nó chứa trạng thái thế giới, hành động đã dẫn đến trạng thái này,
        /// chi phí để đến đây, và node cha của nó.
        /// </summary>
        private class PlanNode
        {
            public PlanNode Parent;
            public float Cost; // g(n): Chi phí từ điểm bắt đầu đến node hiện tại
            public Dictionary<string, object> State;
            public GOAPActionNode Action;

            public PlanNode(PlanNode parent, float cost, Dictionary<string, object> state, GOAPActionNode action)
            {
                Parent = parent;
                Cost = cost;
                State = state;
                Action = action;
            }
        }

        /// <summary>
        /// Tìm một chuỗi các hành động (một kế hoạch) để đi từ trạng thái bắt đầu đến trạng thái mục tiêu.
        /// </summary>
        /// <param name="agent">GameObject đang chạy AI, dùng cho các điều kiện cần ngữ cảnh (như khoảng cách).</param>
        /// <param name="startState">Trạng thái thế giới ban đầu, được lấy từ Blackboard.</param>
        /// <param name="goal">Danh sách các điều kiện định nghĩa trạng thái mục tiêu.</param>
        /// <param name="availableActions">Tất cả các hành động mà AI có thể thực hiện.</param>
        /// <returns>Một danh sách các GOAPActionNode (kế hoạch), hoặc null nếu không tìm thấy kế hoạch nào.</returns>
        public List<GOAPActionNode> FindPlan(GameObject agent, Blackboard blackboard,Dictionary<string, object> startState, List<IGoapPrecondition> goal, List<GOAPActionNode> availableActions)
        {
            // === DEBUG: IN RA MỤC TIÊU CẦN ĐẠT ===
            var goalDescriptions = goal.Select(g => g.GetDescription());
            Debug.Log($"[Planner] Starting search for goal: [ {string.Join(" AND ", goalDescriptions)} ]");

            var usableActions = new HashSet<GOAPActionNode>(availableActions);
            var openSet = new List<PlanNode>();
            var closedSet = new HashSet<PlanNode>();

            // Bắt đầu với một node gốc không có hành động, chi phí bằng 0 và trạng thái ban đầu
            openSet.Add(new PlanNode(null, 0, startState, null));

            // Vòng lặp chính của thuật toán A*
            while (openSet.Count > 0)
            {
                // Tìm node trong openSet có chi phí f(n) = g(n) + h(n) thấp nhất
                // g(n) là currentNode.Cost
                // h(n) là Heuristic()
                var currentNode = openSet.OrderBy(n => n.Cost + Heuristic(agent,blackboard, n.State, goal)).First();
                
                openSet.Remove(currentNode);
                closedSet.Add(currentNode);

                // === DEBUG: KIỂM TRA XEM ĐÃ ĐẠT MỤC TIÊU CHƯA ===
                if (ArePreconditionsMet(agent,blackboard, goal, currentNode.State, isGoalCheck: true))
                {
                    Debug.Log("<color=lime>[Planner] Goal Met! Reconstructing plan.</color>");
                    // Nếu đã đạt mục tiêu, xây dựng lại kế hoạch bằng cách đi ngược từ node hiện tại về gốc
                    var plan = new List<GOAPActionNode>();
                    var n = currentNode;
                    while (n.Parent != null)
                    {
                        plan.Insert(0, n.Action);
                        n = n.Parent;
                    }
                    return plan;
                }
                
                // === DEBUG: XEM XÉT CÁC HÀNH ĐỘNG KHẢ THI TỪ NODE HIỆN TẠI ===
                Debug.Log($"[Planner] Exploring node (Cost: {currentNode.Cost}). Checking {usableActions.Count} available actions...");
                foreach (var action in usableActions)
                {
                    Debug.Log($"-- [Planner] Evaluating action: '{action.name}' (Cost: {action.cost})");

                    // Kiểm tra xem các điều kiện tiên quyết của hành động có được đáp ứng bởi trạng thái hiện tại không
                    if (ArePreconditionsMet(agent,blackboard, action.preconditions, currentNode.State))
                    {
                        // Nếu đáp ứng, tạo ra một trạng thái thế giới mới sau khi thực hiện hành động
                        Debug.Log($"<color=cyan>-- [Planner] Action '{action.name}' is VALID. Creating next state.</color>");
                        var nextState = ApplyEffects(currentNode.State, action.effects);
                        
                        // Tạo một node mới cho kế hoạch và thêm vào openSet để xem xét ở các vòng lặp sau
                        var neighborNode = new PlanNode(currentNode, currentNode.Cost + action.cost, nextState, action);
                        openSet.Add(neighborNode);
                    }
                    else
                    {
                        // Dòng này rất quan trọng để biết tại sao một hành động bị loại bỏ
                        Debug.Log($"-- [Planner] Action '{action.name}' is INVALID. Preconditions not met.");
                    }
                }
            }

            Debug.LogWarning("[Planner] Search finished. No valid plan found because the open set is empty.");
            return null;
        }
        
        /// <summary>
        /// Kiểm tra xem một tập hợp các điều kiện có được đáp ứng bởi một trạng thái thế giới cụ thể không.
        /// </summary>
        private bool ArePreconditionsMet(GameObject agent,Blackboard blackboard, List<IGoapPrecondition> conditions, Dictionary<string, object> worldState, bool isGoalCheck = false)
        {
            string logPrefix = isGoalCheck ? "[Goal Check]" : "[Action Precondition Check]";

            foreach (var condition in conditions)
            {
                bool met = condition.IsMet(agent, blackboard , worldState);
                if (!met)
                {
                    // === DEBUG: IN RA CHÍNH XÁC ĐIỀU KIỆN NÀO BỊ LỖI ===
                    Debug.Log($"<color=red>{logPrefix} FAILED:</color> {condition.GetDescription()}");
                    return false;
                }
                // (Tùy chọn) Bỏ comment dòng dưới nếu muốn xem cả các điều kiện đã thành công
                // else
                // {
                //     Debug.Log($"{logPrefix} PASSED: {condition.GetDescription()}");
                // }
            }
            return true;
        }

        /// <summary>
        /// Áp dụng các hiệu ứng (effects) của một hành động để tạo ra một trạng thái thế giới mới.
        /// </summary>
        private Dictionary<string, object> ApplyEffects(Dictionary<string, object> state, List<GOAPState> effects)
        {
            // Tạo một bản sao của trạng thái hiện tại để không làm thay đổi trạng thái gốc
            var newState = new Dictionary<string, object>(state);
            foreach (var effect in effects)
            {
                // Ghi đè hoặc thêm giá trị mới vào trạng thái
                newState[effect.key] = effect.GetValue();
            }
            return newState;
        }
        
        /// <summary>
        /// Hàm heuristic (hàm ước tính) của thuật toán A*.
        /// Nó ước tính "khoảng cách" còn lại từ trạng thái hiện tại đến mục tiêu.
        /// Một hàm heuristic đơn giản là đếm số điều kiện mục tiêu chưa được đáp ứng.
        /// </summary>
        private float Heuristic(GameObject agent,Blackboard blackboard,  Dictionary<string, object> state, List<IGoapPrecondition> goal)
        {
            return goal.Count(condition => !condition.IsMet(agent,blackboard, state));
        }
    }
}