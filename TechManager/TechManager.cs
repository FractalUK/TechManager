/*
 * The MIT License (MIT)

Copyright (c) 2014 anonish@http://forum.kerbalspaceprogram.com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
 */

/* This source code was modified by Fractal_UK */


using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TechManager
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class TechManager : MonoBehaviour
    {
        static ConfigNode cfgFile;

        static bool prepForCreation = false;
        static bool complexActive = false;
        static bool createNewTree = false;

        static RDController controller;
        static GameObject prefabNode;

        static RDNode[] stockNodes;
        static Dictionary<string, string> stockTechRequired;
        static Dictionary<string, GameObject> newNodes = new Dictionary<string, GameObject>();

        static bool renderWindow = false;
        static GUIStyle listStyle;
        static ComboBox comboBoxControl;
        static IEnumerable<ConfigNode> techConfigs;
        static string lockID = ")*Y)YHGOHGO)";

        void Start()
        {
            DontDestroyOnLoad(this.gameObject);
            GameEvents.onGameSceneLoadRequested.Add(new EventData<GameScenes>.OnEvent(OnGameSceneLoadRequested));
            GameEvents.onGUIRnDComplexSpawn.Add(new EventVoid.OnEvent(OnGUIRnDComplexSpawn));
            GameEvents.onGUIRnDComplexDespawn.Add(new EventVoid.OnEvent(OnGUIRnDComplexDespawn));
        }

        void OnDestroy()
        {
            GameEvents.onGameSceneLoadRequested.Remove(new EventData<GameScenes>.OnEvent(OnGameSceneLoadRequested));
            GameEvents.onGUIRnDComplexSpawn.Remove(new EventVoid.OnEvent(OnGUIRnDComplexSpawn));
            GameEvents.onGUIRnDComplexDespawn.Remove(new EventVoid.OnEvent(OnGUIRnDComplexDespawn));
        }

        void OnGameSceneLoadRequested(GameScenes scene)
        {
            if (scene == GameScenes.MAINMENU)
            {
                // the TechRequired fields of the loaded parts are the only thing modified
                // outside of the particular savegame being loaded. we have to remember
                // what they are set to the first time we load so that we can change it 
                // back when we revisit the main menu to load another game. 
                if (stockTechRequired == null)
                {
                    stockTechRequired = new Dictionary<string, string>();
                    foreach (AvailablePart part in PartLoader.LoadedPartsList)
                    {
                        if (stockTechRequired.ContainsKey(part.name))
                        {
                            print("Skipping duplicate part " + part.name);
                            continue;
                        }
                        stockTechRequired.Add(part.name, part.TechRequired);
                    }
                }
                foreach (AvailablePart part in PartLoader.LoadedPartsList)
                {
                    part.TechRequired = stockTechRequired[part.name];
                }
            }
            if (scene == GameScenes.SPACECENTER)
            {
                ConfigNode techSettingsNode = TechManagerSettings.PluginSettingsFile;
                if (techSettingsNode == null)
                {
                    
                    techConfigs = GameDatabase.Instance.GetConfigNodes("TECHNOLOGY_TREE_DEFINITION").Where(cfg => cfg.HasValue("id"));

                    IDictionary<String, Action<String>> actionDictionary = techConfigs.Select(cfg => new { Key = cfg.GetValue("id"), Value = new Action<String>(str => selectTree(str)) }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    actionDictionary.Add("Stock Tree", new Action<String>(str => selectStockTree(str)));

                    listStyle = new GUIStyle();
                    listStyle.normal.textColor = Color.white;
                    listStyle.onHover.background = listStyle.hover.background = new Texture2D(2, 2);
                    listStyle.padding.left = listStyle.padding.right = listStyle.padding.top = listStyle.padding.bottom = 4;

                    comboBoxControl = new ComboBox(new Rect(Screen.width / 2 - 250, Screen.height / 2, 350, 20), actionDictionary, listStyle);
                    renderWindow = true;
                } else
                {
                    string techTreeID;
                    techTreeID = techSettingsNode.HasValue("techTreeID") ? techSettingsNode.GetValue("techTreeID") : null;
                    Debug.Log("Loading Tech Tree " + techTreeID);
                    cfgFile = GameDatabase.Instance.GetConfigNodes("TECHNOLOGY_TREE_DEFINITION").Where(cfg => cfg.HasValue("id")).FirstOrDefault(cfg => cfg.GetValue("id") == techTreeID);
                }
            }
        }

        void OnGUI()
        {
            if (renderWindow && comboBoxControl != null)
            {
                InputLockManager.SetControlLock(lockID);
                GUILayout.BeginArea(new Rect(Screen.width / 2 - 250, Screen.height / 2 - 30, 500, 60), "TechManager Tree Selector", GUI.skin.window);
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.Label("", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Select", GUILayout.ExpandWidth(false))) comboBoxControl.PerformSelectedAction();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndArea();

                comboBoxControl.Show();
            }
        }

        void selectTree(String tree)
        {
            ConfigNode cfgNode = new ConfigNode();
            cfgNode.AddValue("techTreeID", tree);
            cfgNode.AddValue("useStockTree", false);
            cfgNode.Save(TechManagerSettings.PluginSaveFilePath);
            cfgFile = GameDatabase.Instance.GetConfigNodes("TECHNOLOGY_TREE_DEFINITION").Where(cfg => cfg.HasValue("id")).FirstOrDefault(cfg => cfg.GetValue("id") == tree);
            InputLockManager.RemoveControlLock(lockID);
            renderWindow = false;
        }

        void selectStockTree(String tree)
        {
            ConfigNode cfgNode = new ConfigNode();
            cfgNode.AddValue("useStockTree", true);
            cfgNode.Save(TechManagerSettings.PluginSaveFilePath);
            InputLockManager.RemoveControlLock(lockID);
            renderWindow = false;
        }

        void OnGUIRnDComplexSpawn()
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX) return;
            if (cfgFile == null) return;
            complexActive = true;
            prepForCreation = true;
            createNewTree = true;
        }

        void OnGUIRnDComplexDespawn()
        {
            complexActive = false;
        }

        void Update()
        {
            if (!complexActive) return;

            if (Input.GetKeyDown(KeyCode.F7) && Input.GetKey(KeyCode.LeftAlt))
            {
                createNewTree = true;
            }

            if (prepForCreation)
            {
                prepForCreation = false;
                DeactivateStockTree();
                PrepForCreation();
            }

            if (createNewTree && cfgFile != null)
            {
                createNewTree = false;
                RemoveNewNodes();
                AssignParts();
                UpdateTechState();
                AddNewNodes();
            }
        }

        static void DeactivateStockTree()
        {
            stockNodes = GameObject.FindObjectsOfType<RDNode>();
            foreach (RDNode rdnode in stockNodes)
            {
                rdnode.gameObject.SetActive(false);
            }
        }

        static void PrepForCreation()
        {
            // the nodes we created last time no longer exist, clear our list
            newNodes.Clear();

            controller = (RDController)GameObject.FindObjectOfType(typeof(RDController));

            // lets start the view of the tech tree in a more reasonable place
            typeof(RDGridArea).GetMethod("ZoomTo", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller.gridArea, new object[] { 1f, true });

            RDNode startNode = Array.Find<RDNode>(stockNodes, x => x.gameObject.name == "node0_start");
            if (startNode == null) startNode = stockNodes[0];

            if (prefabNode != null) DestroyImmediate(prefabNode);

            prefabNode = new GameObject("prefabTechObject");
            prefabNode.SetActive(false);
            prefabNode.AddComponent<RDTech>();
            prefabNode.AddComponent<RDNode>();

            prefabNode.GetComponent<RDNode>().controller = controller;
            prefabNode.GetComponent<RDNode>().scale = startNode.scale;
            prefabNode.GetComponent<RDNode>().prefab = startNode.prefab;
            prefabNode.transform.parent = startNode.transform.parent;
            prefabNode.transform.localPosition = startNode.transform.localPosition;
        }

        static void RemoveNewNodes()
        {
            foreach (GameObject obj in newNodes.Values)
            {
                DestroyImmediate(obj);
            }
            newNodes.Clear();
        }

        static void AssignParts()
        {
            // tech name assigned to a specific part
            Dictionary<string, string> techAssigned = new Dictionary<string, string>();

            foreach (ConfigNode cfgNode in cfgFile.GetNodes("NODE"))
            {
                if (!cfgNode.HasNode("PARTS")) continue;

                string techID = cfgNode.GetValue("techID");

                foreach (string partname in cfgNode.GetNode("PARTS").GetValues("name"))
                {
                    if (techAssigned.ContainsKey(partname))
                    {
                        print("Skipping duplicate assignment for part: " + partname);
                        continue;
                    }
                    techAssigned.Add(partname, techID);
                }
            }

            foreach (AvailablePart part in PartLoader.LoadedPartsList)
            {
                string techID = stockTechRequired[part.name];

                // parts assigned to techs via the cfg file have priority
                if (techAssigned.ContainsKey(part.name))
                {
                    techID = techAssigned[part.name];
                }

                part.TechRequired = techID;
            }
        }

        static void UpdateTechState()
        {
            foreach (ConfigNode cfgNode in cfgFile.GetNodes("NODE"))
            {
                string techID = cfgNode.GetValue("techID");
                if (string.IsNullOrEmpty(techID)) continue;

                ProtoTechNode techState = ResearchAndDevelopment.Instance.GetTechState(techID);
                if (techState == null)
                {
                    techState = new ProtoTechNode();
                    techState.techID = techID;
                    techState.state = RDTech.State.Unavailable;
                    techState.partsPurchased = new List<AvailablePart>();
                    ResearchAndDevelopment.Instance.SetTechState(techID, techState);
                }

                techState.partsPurchased.RemoveAll(x => x.TechRequired != techID);
            }
        }

        static void AddNewNodes()
        {
            Dictionary<string, string[]> parentList = new Dictionary<string, string[]>();

            foreach (ConfigNode cfgNode in cfgFile.GetNodes("NODE"))
            {
                string name = cfgNode.GetValue("name");
                if (String.IsNullOrEmpty(name)) continue;

                if (!name.StartsWith("newnode_"))
                {
                    name = "newnode_" + name;
                }

                if (newNodes.ContainsKey(name))
                {
                    print("Skipping duplicate node: " + name);
                    continue;
                }

                if (!cfgNode.HasValue("techID"))
                {
                    print("node " + name + " needs a techID to be defined");
                    continue;
                }

                if (!cfgNode.HasValue("title"))
                {
                    print("node " + name + " needs a title to be defined");
                    continue;
                }

                GameObject newNodeObj = (GameObject)GameObject.Instantiate(prefabNode);
                newNodeObj.name = name;
                newNodeObj.transform.parent = prefabNode.transform.parent;
                newNodeObj.transform.localPosition = prefabNode.transform.localPosition;

                RDNode newNode = newNodeObj.GetComponent<RDNode>();
                newNode.icon = RDNode.Icon.GENERIC;
                newNode.AnyParentToUnlock = true;

                RDTech newTech = newNodeObj.GetComponent<RDTech>();
                newTech.techID = cfgNode.GetValue("techID");
                newTech.title = cfgNode.GetValue("title");
                newTech.description = "kinda boring, really";
                newTech.scienceCost = 0;
                newTech.hideIfNoParts = false;

                if (cfgNode.HasValue("pos"))
                {
                    newNode.transform.localPosition = ConfigNode.ParseVector3(cfgNode.GetValue("pos"));
                }
                if (cfgNode.HasValue("icon"))
                {
                    newNode.icon = (RDNode.Icon)ConfigNode.ParseEnum(typeof(RDNode.Icon), cfgNode.GetValue("icon"));
                }
                if (cfgNode.HasValue("anyParent"))
                {
                    newNode.AnyParentToUnlock = bool.Parse(cfgNode.GetValue("anyParent"));
                }
                if (cfgNode.HasValue("description"))
                {
                    newTech.description = cfgNode.GetValue("description");
                }
                if (cfgNode.HasValue("cost"))
                {
                    newTech.scienceCost = int.Parse(cfgNode.GetValue("cost"));
                }
                if (cfgNode.HasValue("hideIfEmpty"))
                {
                    newTech.hideIfNoParts = bool.Parse(cfgNode.GetValue("hideIfEmpty"));
                }
                if (cfgNode.HasValue("parents"))
                {
                    string parents = cfgNode.GetValue("parents").Replace(',', ' ');
                    string[] parent_list = parents.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parent_list.Length; i++)
                    {
                        if (parent_list[i].StartsWith("newnode_")) continue;
                        parent_list[i] = "newnode_" + parent_list[i];
                    }
                    parentList.Add(name, parent_list);
                }

                newNodes.Add(name, newNodeObj);
            }

            RDNode.Anchor childAnchor = new RDNode.Anchor();
            RDNode.Anchor parentAnchor = new RDNode.Anchor();
            foreach (string child in parentList.Keys)
            {
                List<RDNode.Parent> parents = new List<RDNode.Parent>();
                foreach (string parent in parentList[child])
                {
                    if (!newNodes.ContainsKey(parent)) continue;
                    Vector3 relPos = newNodes[child].gameObject.transform.position - newNodes[parent].gameObject.transform.position;
                    getAnchors(relPos, ref childAnchor, ref parentAnchor);
                    parents.Add(new RDNode.Parent(new RDNode.ParentAnchor(newNodes[parent].GetComponent<RDNode>(), parentAnchor), childAnchor));
                }
                newNodes[child].GetComponent<RDNode>().parents = parents.ToArray();
            }

            foreach (GameObject obj in newNodes.Values)
            {
                obj.GetComponent<RDTech>().Start();
                obj.SetActive(true);
            }
        }

        static void getAnchors(Vector3 relPos, ref RDNode.Anchor childAnchor, ref RDNode.Anchor parentAnchor)
        {
            // default in case nothing else matches
            parentAnchor = RDNode.Anchor.RIGHT;
            childAnchor = RDNode.Anchor.LEFT;

            // child is more below than left or right
            // ksp can not draw arrows from BOTTOM to TOP, so force an alternative
            if (-relPos.y > Mathf.Abs(relPos.x))
            {
                relPos.y = 0;
            }

            // child is more above than left or right
            if (relPos.y > Mathf.Abs(relPos.x))
            {
                parentAnchor = RDNode.Anchor.TOP;
                childAnchor = RDNode.Anchor.BOTTOM;
            }

            // child is more to the left than above or below
            // these aren't very pretty, but they 'work'
            if (-relPos.x >= Mathf.Abs(relPos.y))
            {
                parentAnchor = RDNode.Anchor.LEFT;
                childAnchor = RDNode.Anchor.RIGHT;
            }
        }

    }

} // namespace

