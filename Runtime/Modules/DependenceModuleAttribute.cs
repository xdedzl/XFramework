using System;

namespace XFramework
{
    public class DependenceModuleAttribute : Attribute
    {
        public Type moduleType;
        public object[] args;

        public DependenceModuleAttribute(Type moduleType, params object[] args)
        {
            this.moduleType = moduleType;
            this.args = args;
        }
    }
}