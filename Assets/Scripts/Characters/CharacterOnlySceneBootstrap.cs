using UnityEngine;

namespace Woken.Characters
{
    /// <summary>Builds a simple character test scene without level geometry.</summary>
    public sealed class CharacterOnlySceneBootstrap : MonoBehaviour
    {
        private static Sprite unitSprite;

        private void Awake()
        {
            SetupCamera();
            CreateFloor();
            CreateCharacter("Oyuncu", new Vector3(-1.1f, 0f, 0f), false);
            CreateCharacter("Robot", new Vector3(1.1f, 0f, 0f), true);
        }

        private static void SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.16f, 0.19f, 0.22f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private static void CreateFloor()
        {
            var floor = new GameObject("Floor");
            floor.transform.localScale = new Vector3(24f, 16f, 1f);
            var renderer = floor.AddComponent<SpriteRenderer>();
            renderer.sprite = GetUnitSprite();
            renderer.color = new Color32(235, 230, 216, 255);
            renderer.sortingOrder = -10;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) renderer.material = new Material(shader);
        }

        private static Sprite GetUnitSprite()
        {
            if (unitSprite != null) return unitSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = "Character test floor texture"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            unitSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
            return unitSprite;
        }

        private static void CreateCharacter(string objectName, Vector3 position, bool isRobot)
        {
            var character = new GameObject(objectName);
            character.transform.position = position;
            character.transform.localScale = Vector3.one;

            var renderer = character.AddComponent<SpriteRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) renderer.material = new Material(shader);
            renderer.sortingOrder = isRobot ? 1 : 0;

            var animator = character.AddComponent<CharacterAnimator>();
            animator.Initialize(isRobot);

            if (!isRobot)
            {
                var body = character.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                character.AddComponent<CapsuleCollider2D>();
                character.AddComponent<PlayerWASDController>();
            }
        }
    }
}
