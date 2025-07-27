using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    [Serializable] public class IntParameter : GenericParameter<int> {}
    [Serializable] public class FloatParameter : GenericParameter<float> {}
    [Serializable] public class BoolParameter : GenericParameter<bool> {}
    [Serializable] public class StringParameter : GenericParameter<string> {}

    // Unity Core Types
    [Serializable] public class Vector2Parameter : GenericParameter<Vector2> {}
    [Serializable] public class Vector3Parameter : GenericParameter<Vector3> {}
    [Serializable] public class Vector4Parameter : GenericParameter<Vector4> {}
    [Serializable] public class QuaternionParameter : GenericParameter<Quaternion> {}
    [Serializable] public class ColorParameter : GenericParameter<Color> {}
    [Serializable] public class RectParameter : GenericParameter<Rect> {}
    [Serializable] public class BoundsParameter : GenericParameter<Bounds> {}

    // GameObject & Components
    [Serializable] public class GameObjectParameter : GenericParameter<GameObject> {}
    [Serializable] public class TransformParameter : GenericParameter<Transform> {}
    [Serializable] public class ComponentParameter : GenericParameter<Component> {}
    [Serializable] public class RigidbodyParameter : GenericParameter<Rigidbody> {}
    [Serializable] public class ColliderParameter : GenericParameter<Collider> {}
    [Serializable] public class AnimatorParameter : GenericParameter<Animator> {}
    [Serializable] public class CameraParameter : GenericParameter<Camera> {}
    [Serializable] public class LightParameter : GenericParameter<Light> {}
    [Serializable] public class RectTransformParameter : GenericParameter<RectTransform> {}

    // Rendering & Materials
    [Serializable] public class MaterialParameter : GenericParameter<Material> {}
    [Serializable] public class ShaderParameter : GenericParameter<Shader> {}
    [Serializable] public class TextureParameter : GenericParameter<Texture> {}
    [Serializable] public class Texture2DParameter : GenericParameter<Texture2D> {}
    [Serializable] public class RenderTextureParameter : GenericParameter<RenderTexture> {}
    [Serializable] public class SpriteParameter : GenericParameter<Sprite> {}

    // Audio
    [Serializable] public class AudioClipParameter : GenericParameter<AudioClip> {}

    // Miscellaneous
    [Serializable] public class LayerMaskParameter : GenericParameter<LayerMask> {}
    [Serializable] public class AnimationCurveParameter : GenericParameter<AnimationCurve> {}
    [Serializable] public class GradientParameter : GenericParameter<Gradient> {}
    [Serializable] public class KeyCodeParameter : GenericParameter<KeyCode> {}


    //Test for a random struct
    [Serializable] public class DamageInfoParameter : GenericParameter<DamageInfo> {}
