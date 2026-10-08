using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;

namespace FogHarbor.LuaLab
{
    /// <summary>Scene-local Lua runtime used only by LuaLab.</summary>
    public sealed class LuaEnvManager : MonoBehaviour
    {
        private LuaEnv environment;
        private readonly HashSet<LuaBehaviour> clients = new HashSet<LuaBehaviour>();
        public int ReloadCount { get; private set; }
        public string LastReloadMessage { get; private set; } = "F5 / Reload Lua: read saved Lua files";
        public LuaEnv Environment
        {
            get
            {
                if (environment == null)
                {
                    environment = new LuaEnv();
                    environment.AddLoader(LoadModule);
                }
                return environment;
            }
        }

        private byte[] LoadModule(ref string moduleName)
        {
            if (moduleName.Contains("..") || moduleName.Contains("/") || moduleName.Contains("\\"))
                throw new ArgumentException("LuaLab module names must use dotted identifiers.");
            string root = Path.Combine(Application.streamingAssetsPath, "LuaLab");
            string path = Path.Combine(root, moduleName.Replace('.', Path.DirectorySeparatorChar) + ".lua");
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.WebGLPlayer)
                throw new PlatformNotSupportedException("LuaLab currently uses a desktop file loader.");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public LuaTable CreateInstance(string moduleName, PatrolMotor motor)
        {
            using (var require = Environment.Global.Get<LuaFunction>("require"))
            {
                object[] results = require.Call(moduleName);
                using (var module = (LuaTable)results[0])
                using (var create = module.Get<LuaFunction>("new"))
                    return (LuaTable)create.Call(motor)[0];
            }
        }

        // Stage every replacement before changing any object's cached functions.
        public bool ReloadAll()
        {
            var targets = new List<LuaBehaviour>(clients);
            targets.RemoveAll(client => client == null || !client.IsLoaded);
            if (targets.Count == 0)
            {
                LastReloadMessage = "No loaded Lua behaviours to reload.";
                return false;
            }
            var oldModules = new Dictionary<string, LuaTable>();
            var newModules = new Dictionary<string, LuaTable>();
            using (var package = Environment.Global.Get<LuaTable>("package"))
            using (var loaded = package.Get<LuaTable>("loaded"))
            using (var require = Environment.Global.Get<LuaFunction>("require"))
            {
                try
                {
                    foreach (var client in targets)
                    {
                        string name = client.ModuleName;
                        if (newModules.ContainsKey(name)) continue;
                        oldModules.Add(name, loaded.Get<LuaTable>(name));
                        loaded.Set<string, object>(name, null);
                        object[] result = require.Call(name);
                        var module = result.Length > 0 ? result[0] as LuaTable : null;
                        if (module == null) throw new InvalidOperationException(name + " must return a module table.");
                        newModules.Add(name, module);
                    }
                    foreach (var client in targets) client.PrepareReload(newModules[client.ModuleName]);
                    foreach (var client in targets) client.CommitReload();
                    ReloadCount++;
                    LastReloadMessage = "Reload #" + ReloadCount + " OK (" + targets.Count + " object(s))";
                    return true;
                }
                catch (Exception error)
                {
                    foreach (var client in targets) client.CancelReload();
                    foreach (var old in oldModules) loaded.Set(old.Key, old.Value);
                    LastReloadMessage = "Reload failed; previous behaviour kept: " + error.Message;
                    Debug.LogWarning("[LuaLab] " + LastReloadMessage, this);
                    return false;
                }
                finally
                {
                    foreach (var module in newModules.Values) module.Dispose();
                    foreach (var module in oldModules.Values) module?.Dispose();
                }
            }
        }

        internal void Register(LuaBehaviour client) { clients.Add(client); }
        internal void Unregister(LuaBehaviour client) { clients.Remove(client); }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5)) ReloadAll();
            if (environment != null) environment.Tick();
        }

        private void OnDestroy()
        {
            foreach (var client in new List<LuaBehaviour>(clients))
                if (client != null) client.Release();
            clients.Clear();
            if (environment == null) return;
            environment.FullGc();
            environment.Dispose();
            environment = null;
        }
    }
}