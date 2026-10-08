using System;
using UnityEngine;
using XLua;

namespace FogHarbor.LuaLab
{
    [RequireComponent(typeof(PatrolMotor))]
    public sealed class LuaBehaviour : MonoBehaviour
    {
        [SerializeField] private LuaEnvManager runtime;
        [SerializeField] private string luaScriptName = "patrol_ai";
        private LuaTable instance;
        private LuaFunction update;
        private LuaFunction shutdown;
        private LuaTable pendingInstance;
        private LuaFunction pendingUpdate;
        private LuaFunction pendingShutdown;
        private PatrolMotor motor;
        public bool IsLoaded => instance != null;
        public string ModuleName => luaScriptName;
        public int UpdateCount { get; private set; }
        public int BindingVersion { get; private set; }

        private void Start()
        {
            motor = GetComponent<PatrolMotor>();
            if (runtime == null) runtime = FindObjectOfType<LuaEnvManager>();
            try
            {
                if (runtime == null) throw new InvalidOperationException("LuaLab requires a LuaEnvManager.");
                runtime.Register(this);
                instance = runtime.CreateInstance(luaScriptName, motor);
                update = instance.Get<LuaFunction>("update");
                shutdown = instance.Get<LuaFunction>("shutdown");
                if (update == null) throw new InvalidOperationException("Lua module requires update(dt).");
                BindingVersion = 1;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Release();
                enabled = false;
            }
        }

        // new(motor, savedState) must construct closures without modifying Unity objects.
        internal void PrepareReload(LuaTable module)
        {
            CancelReload();
            using (var save = instance.Get<LuaFunction>("save_state"))
            {
                LuaTable snapshot = null;
                try
                {
                    if (save != null)
                    {
                        var values = save.Call();
                        snapshot = values.Length > 0 ? values[0] as LuaTable : null;
                    }
                    using (var create = module.Get<LuaFunction>("new"))
                    {
                        if (create == null) throw new InvalidOperationException(ModuleName + " requires new(motor, state).");
                        var values = create.Call(motor, snapshot);
                        pendingInstance = values.Length > 0 ? values[0] as LuaTable : null;
                    }
                    if (pendingInstance == null) throw new InvalidOperationException("new() must return an instance table.");
                    pendingUpdate = pendingInstance.Get<LuaFunction>("update");
                    pendingShutdown = pendingInstance.Get<LuaFunction>("shutdown");
                    if (pendingUpdate == null) throw new InvalidOperationException("Lua module requires update(dt).");
                }
                finally { snapshot?.Dispose(); }
            }
        }

        internal void CommitReload()
        {
            // Old shutdown is reserved for final destruction, since it stops the motor.
            update?.Dispose();
            shutdown?.Dispose();
            instance?.Dispose();
            instance = pendingInstance;
            update = pendingUpdate;
            shutdown = pendingShutdown;
            pendingInstance = null;
            pendingUpdate = null;
            pendingShutdown = null;
            BindingVersion++;
        }

        internal void CancelReload()
        {
            pendingUpdate?.Dispose();
            pendingShutdown?.Dispose();
            pendingInstance?.Dispose();
            pendingUpdate = null;
            pendingShutdown = null;
            pendingInstance = null;
        }

        private void Update()
        {
            if (update == null) return;
            try
            {
                update.Call(Time.deltaTime);
                UpdateCount++;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Release();
                enabled = false;
            }
        }

        public void Release()
        {
            try { if (shutdown != null) shutdown.Call(); }
            catch (Exception error) { Debug.LogException(error, this); }
            finally
            {
                CancelReload();
                update?.Dispose();
                shutdown?.Dispose();
                instance?.Dispose();
                update = null;
                shutdown = null;
                instance = null;
                if (motor != null) motor.Stop();
                if (runtime != null) runtime.Unregister(this);
            }
        }

        private void OnDisable() { if (motor != null) motor.Stop(); }
        private void OnDestroy() { Release(); }
    }
}