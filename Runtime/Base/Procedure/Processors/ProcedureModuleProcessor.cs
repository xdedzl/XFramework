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
        public void OnRefreshProcedureState(ProcedureBase procedure, ProcedureAttributeContext subContext, ProcedureAttributeContext parentContext)
        {
            var requiredTypes = new HashSet<Type>();
            var attr = subContext?.ModuleAttr ?? parentContext?.ModuleAttr;

            if (attr != null)
            {
                foreach (var moduleType in attr.ModuleTypes)
                {
                    var lifecycleAttr = moduleType.GetCustomAttribute<ModuleLifecycleAttribute>();
                    if (lifecycleAttr == null || lifecycleAttr.Lifecycle != ModuleLifecycle.Procedure)
                    {
                        throw new XFrameworkException(
                            $"[Procedure] Module {moduleType.Name} declared in ProcedureModuleAttribute on {procedure.GetType().Name} " +
                            $"must have [ModuleLifecycle(ModuleLifecycle.Procedure)] attribute");
                    }
                    requiredTypes.Add(moduleType);
                }
            }

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
    }
}
