using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace TechManager
{
    public class ComboBox
    {
        private static bool forceToUnShow = false;
        private static int useControlID = -1;
        private bool isClickedComboButton = false;
        private int selectedItemIndex = 0;

        private Rect rect;
        private GUIContent buttonContent;
        private GUIContent[] listContent;
        private string buttonStyle;
        private string boxStyle;
        private GUIStyle listStyle;
        private IDictionary<String, Action<String>> actionDictionary;

        public int SelectedItemIndex
        {
            get { return selectedItemIndex; }
            private set { selectedItemIndex = value; }
        }

        public ComboBox(Rect rect, IDictionary<String, Action<String>> actionDictionary, GUIStyle listStyle)
        {
            this.rect = rect;
            this.buttonStyle = "button";
            this.boxStyle = "box";
            this.listStyle = listStyle;
            this.actionDictionary = actionDictionary;
            this.listContent = actionDictionary.Keys.Select(ky => new GUIContent(ky)).ToArray();
            this.buttonContent = listContent.FirstOrDefault();
        }

        public int Show()
        {
            if (forceToUnShow)
            {
                forceToUnShow = false;
                isClickedComboButton = false;
            }

            bool done = false;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (Event.current.GetTypeForControl(controlID))
            {
                case EventType.mouseUp:
                {
                    if (isClickedComboButton)
                    {
                        done = true;
                    }
                }
                break;
            }

            if (GUI.Button(rect, buttonContent, buttonStyle))
            {
                if (useControlID == -1)
                {
                    useControlID = controlID;
                    isClickedComboButton = false;
                }

                if (useControlID != controlID)
                {
                    forceToUnShow = true;
                    useControlID = controlID;
                }
                isClickedComboButton = true;
            }

            if (isClickedComboButton)
            {
                Rect listRect = new Rect(rect.x, rect.y + listStyle.CalcHeight(listContent[0], 1.0f),
                          rect.width, listStyle.CalcHeight(listContent[0], 1.0f) * listContent.Length);

                GUI.Box(listRect, "", boxStyle);
                int newSelectedItemIndex = GUI.SelectionGrid(listRect, selectedItemIndex, listContent, 1, listStyle);
                if (newSelectedItemIndex != selectedItemIndex)
                {
                    selectedItemIndex = newSelectedItemIndex;
                    buttonContent = listContent[selectedItemIndex];
                }
            }

            if (done)
                isClickedComboButton = false;

            return selectedItemIndex;
        }

        public void PerformSelectedAction()
        {
            GUIContent selectedContent = listContent[this.selectedItemIndex];
            KeyValuePair<String, Action<String>> keyvalue = this.actionDictionary.FirstOrDefault(kvp => kvp.Key == selectedContent.text);
            if (keyvalue.Value != null && keyvalue.Key != null) keyvalue.Value(keyvalue.Key);
        }
    }
}

