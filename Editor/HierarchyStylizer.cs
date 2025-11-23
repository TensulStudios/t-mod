#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TABMM
{
    public class StyleHierarchy
    {
        public string icon;
        public string customText;
        public bool useCustomName;
        public Color backRectColor;
        public Color textColor = Color.white;
        Texture2D _icon;

        public StyleHierarchy(string icon, Color backRectColor, Color textColor, string customText = "", bool useCustomName = false)
        {
            _icon = AssetDatabase.LoadAssetAtPath<Texture2D>(icon);

            if (_icon == null)
                Debug.LogWarning("Failed to load icon texture");
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            this.icon = icon;
            this.customText = customText;
            this.useCustomName = useCustomName;
            this.backRectColor = backRectColor;
            this.textColor = textColor;
        }

        void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (go == null) return;

            var mm = go.GetComponent<MapDescriptor>();

            if (mm != null)
            {
                float size = 32;
                float iconWidth = size / 1.5f;
                float iconHeight = size / 1.5f;

                Rect iconRect = new Rect(selectionRect.xMin + 30, selectionRect.y + (selectionRect.height - iconHeight) / 2, iconWidth, iconHeight);

                iconRect.x -= 55;

                float highlightWidth = selectionRect.width;
                Rect highlightRect = new Rect(selectionRect.xMin, selectionRect.y, highlightWidth, selectionRect.height);
                highlightRect.x -= 28;
                highlightRect.x -= 50;
                highlightRect.xMax += 150;
                if (Selection.objects.Contains((Object)go))
                    EditorGUI.DrawRect(highlightRect, backRectColor + new Color(0.1725490196f, 0.36470588235f, 0.5294117647f));
                else
                    EditorGUI.DrawRect(highlightRect, backRectColor);
                highlightRect.x += 50;

                GUIStyle style = new GUIStyle();
                style.normal.textColor = textColor;
                highlightRect.x += 30;
                if (useCustomName)
                    EditorGUI.LabelField(highlightRect, customText, style);
                else
                    EditorGUI.LabelField(highlightRect, go.transform.name, style);

                GUI.DrawTexture(iconRect, _icon);
            }
        }
    }

    public class StyleHierarchyByName
    {
        public string targetName;
        public string icon;
        public Color backRectColor;
        public Color textColor = Color.white;
        Texture2D _icon;

        public StyleHierarchyByName(string targetName, string icon, Color backRectColor, Color textColor)
        {
            this.targetName = targetName;
            this.icon = icon;
            this.backRectColor = backRectColor;
            this.textColor = textColor;

            _icon = AssetDatabase.LoadAssetAtPath<Texture2D>(icon);
            if (_icon == null)
                Debug.LogWarning("Failed to load icon texture");

            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
        }

        void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            var go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (go == null || go.name != targetName) return;

            float size = 32;
            float iconWidth = size;
            float iconHeight = size / 1.5f;
            Rect iconRect = new Rect(selectionRect.xMin + 30, selectionRect.y + (selectionRect.height - iconHeight) / 2, iconWidth, iconHeight);
            iconRect.x -= 55;

            float highlightWidth = selectionRect.width;
            Rect highlightRect = new Rect(selectionRect.xMin, selectionRect.y, highlightWidth, selectionRect.height);
            highlightRect.x -= 28;
            highlightRect.x -= 50;
            highlightRect.xMax += 150;

            if (Selection.objects.Contains((Object)go))
                EditorGUI.DrawRect(highlightRect, backRectColor + new Color(0.2745098f, 0.3764706f, 0.4862745f));
            else
                EditorGUI.DrawRect(highlightRect, backRectColor);
            highlightRect.x += 80;

            GUIStyle style = new GUIStyle();

            style.normal.textColor = textColor;

            EditorGUI.LabelField(highlightRect, go.transform.name, style);
            if (_icon)
                GUI.DrawTexture(iconRect, _icon);
        }
    }
}
#endif