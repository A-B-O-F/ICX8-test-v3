using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Operator status as 3D text inside the scene, so it shows on the CAVE walls where the
    /// on-screen panel cannot. F1 shows or hides it. Hide it before a participant starts.</summary>
    public class StatusBoard
    {
        readonly GameObject root;
        readonly TextMesh text;

        public bool Visible { get { return root.activeSelf; } set { root.SetActive(value); } }

        public StatusBoard(Transform parent, Vector3 position, float yawDegrees)
        {
            root = new GameObject("OperatorStatusBoard");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            text = root.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            if (font != null) root.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.fontSize = 64;
            text.characterSize = 0.012f;
            text.anchor = TextAnchor.UpperCenter;
            text.alignment = TextAlignment.Left;
            text.color = new Color(0.85f, 0.95f, 1f);
        }

        public void SetText(string s) { text.text = s; }

        public void Dispose() { if (root != null) Object.Destroy(root); }
    }
}
