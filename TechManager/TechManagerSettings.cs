using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TechManager
{
    static class TechManagerSettings
    {
        public static string PluginSaveFilePath
        {
            get
            {
                return KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/TechManager.cfg";
            }
        }

        public static ConfigNode PluginSettingsFile
        {
            get
            {
                ConfigNode config = ConfigNode.Load(TechManagerSettings.PluginSaveFilePath);
                config = new ConfigNode();
                return config;
            }
        }
    }
}
