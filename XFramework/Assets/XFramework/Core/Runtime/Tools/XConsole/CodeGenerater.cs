using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace XFramework.Console
{
    public class CodeGenerater
    {
        private List<string> nameSpaces = new List<string>();
        private List<Class> classes = new List<Class>();

        public void AddNameSpace(string name)
        {
            nameSpaces.Add(name);
        }

        public void AddNameSpace(IList<string> names)
        {
            nameSpaces.AddRange(names);
        }

        public void AddClass(Class @class)
        {
            classes.Add(@class);
        }

        public string Code
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var item in nameSpaces)
                {
                    stringBuilder.Append($"using {item};\n");
                }
                foreach (var item in classes)
                {
                    stringBuilder.Append(item.Code);
                    stringBuilder.Append("\n");
                }

                return stringBuilder.ToString();
            }
        }
    }

    public class Class
    {
        public string name;
        private readonly List<Variable> variables = new List<Variable>();
        private readonly List<Function> functions = new List<Function>();

        public Class(string name)
        {
            this.name = name;
        }

        public void AddVariable(Variable variable)
        {
            variables.Add(variable);
        }

        public void AddFunction(Function function)
        {
            functions.Add(function);
        }

        public string Code
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();

                stringBuilder.Append($"public class {name}\n{{\n");

                foreach (var item in variables)
                {
                    stringBuilder.Append(item.Code);
                    stringBuilder.Append("\n");
                }

                foreach (var item in functions)
                {
                    stringBuilder.Append(item.Code);
                    stringBuilder.Append("\n");
                }

                stringBuilder.Append("}\n");


                return stringBuilder.ToString();
            }
        }
    }

    public struct Function
    {
        public string name;
        public Type returnType;
        public string[] contents;

        public string Code
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();
                var returnStr = returnType == null ? "void" : returnType.Name;
                stringBuilder.Append($"\tpublic {returnStr} {name}()\n\t{{\n");
                foreach (var item in contents)
                {
                    stringBuilder.Append("\t\t");
                    stringBuilder.Append(item);
                    stringBuilder.Append(";\n");
                }
                stringBuilder.Append("\t}");
                return stringBuilder.ToString();
            }
        }
    }

    public struct Variable
    {
        public Type type;
        public string name;

        public string Code
        {
            get
            {
                return $"\t{type.Name} {name};"; 
            }
        }
    }
}
