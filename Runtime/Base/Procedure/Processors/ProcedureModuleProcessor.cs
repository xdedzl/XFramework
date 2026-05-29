using System;
using System.Collections.Generic;
using System.Reflection;

namespace XFramework
{
    /// <summary>
    /// 流程模块加载处理器
    /// </summary>
    public class ProcedureModuleProcessor : IProcedureProcessor
    {
        public void OnRefreshProcedureState(ProcedureRefreshContext context)
        {
            var requiredTypes = new HashSet<Type>();
            AddRequiredModules(requiredTypes, context.SubContext?.ModuleAttr ?? context.ParentContext?.ModuleAttr);
            AddRequiredModules(requiredTypes, context.OverlayContext?.ModuleAttr);

            var loadedProcedureModules = GameEntry.GetLoadedModuleTypes(ModuleLifecycle.Procedure);

            foreach (var loadedType in loadedProcedureModules)
            {
                if (!requiredTypes.Contains(loadedType))
                {
                    GameEntry.ShutdownModule(loadedType);
                }
            }

            foreach (var requiredType in requiredTypes)
            {
                if (!GameEntry.IsModuleLoaded(requiredType))
                {
                    GameEntry.AddModule(requiredType);
                }
            }
        }

        private void AddRequiredModules(HashSet<Type> requiredTypes, ProcedureModuleAttribute attr)
        {
            if (attr == null)
            {
                return;
            }

            foreach (var moduleType in attr.ModuleTypes)
            {
                var lifecycleAttr = moduleType.GetCustomAttribute<ModuleLifecycleAttribute>();
                if (lifecycleAttr == null || lifecycleAttr.Lifecycle != ModuleLifecycle.Procedure)
                {
                    throw new XFrameworkException(
                        $"[Procedure] Module {moduleType.Name} declared in ProcedureModuleAttribute must have [ModuleLifecycle(ModuleLifecycle.Procedure)] attribute");
                }
                requiredTypes.Add(moduleType);
            }
        }
    }
}
