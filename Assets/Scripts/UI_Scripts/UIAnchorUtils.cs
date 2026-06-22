using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

    public class UIAnchorUtils
    {
#if UNITY_EDITOR
    //For found in visual studio searcher: ANCHOR CTRL SHIFT L Keycode.L
    [MenuItem("UI/Anchor Around Object %l")]
        public static void uGUIAnchorAroundObject()
        {
            uGUIAnchorAroundObjectFinal(null);
        }

        public static void uGUIAnchorAroundObjectFinal(GameObject go = null)
        {
            GameObject gameObjectToUpdateAnchor = null;
            if (go == null)
            {
                gameObjectToUpdateAnchor = Selection.activeGameObject;
            }
            else
            {
                gameObjectToUpdateAnchor = go;
            }

            if (gameObjectToUpdateAnchor != null && gameObjectToUpdateAnchor.GetComponent<RectTransform>() != null)
            {
                Undo.RecordObject(gameObjectToUpdateAnchor, "Reasigned Anchor");
                var r = gameObjectToUpdateAnchor.GetComponent<RectTransform>();
                var p = gameObjectToUpdateAnchor.transform.parent.GetComponent<RectTransform>();

                var offsetMin = r.offsetMin;
                var offsetMax = r.offsetMax;
                var _anchorMin = r.anchorMin;
                var _anchorMax = r.anchorMax;

                var parent_width = p.rect.width;
                var parent_height = p.rect.height;

                var anchorMin = new Vector2(_anchorMin.x + (offsetMin.x / parent_width),
                                            _anchorMin.y + (offsetMin.y / parent_height));
                var anchorMax = new Vector2(_anchorMax.x + (offsetMax.x / parent_width),
                                            _anchorMax.y + (offsetMax.y / parent_height));

                r.anchorMin = anchorMin;
                r.anchorMax = anchorMax;

                r.offsetMin = new Vector2(0, 0);
                r.offsetMax = new Vector2(0, 0);
                r.pivot = new Vector2(0.5f, 0.5f);

            }
            else
            {
                Debug.LogError("Trying to move object without RectTransform");
            }
        }

        [MenuItem("UI/Anchor Around Object All %#K")]
        static void uGUIAnchorAroundObjectAll()
        {
            List<Transform> gameObjectsToUpdate = new List<Transform>();

            foreach (GameObject go in Selection.objects)
            {
                if (!gameObjectsToUpdate.Contains(go.transform))
                {
                    gameObjectsToUpdate.Add(go.transform);
                    Transform[] childrensGameObjects = go.GetComponentsInChildren<Transform>();
                    foreach (Transform childrenGo in childrensGameObjects)
                    {
                        if (!gameObjectsToUpdate.Contains(childrenGo))
                        {
                            if (childrenGo.parent != null && !childrenGo.parent.GetComponent<TMPro.TextMeshProUGUI>())
                                gameObjectsToUpdate.Add(childrenGo);
                        }
                    }
                }
            }
            foreach (Transform go in gameObjectsToUpdate)
            {
                uGUIAnchorAroundObjectFinal(go.gameObject);
            }

        }
#endif
    }
