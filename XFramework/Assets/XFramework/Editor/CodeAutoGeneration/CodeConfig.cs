using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Quick.Code
{
    public class CodeConfig
    {
        public const string FindBaseGUI = "(this[\"{0}\"] as {1})";
        public const string nameSpcae = "using UnityEngine;\nusing UnityEngine.UI;\nusing XFramework.UI;\n\n";
        public const string classStart = "public class {0} : BasePanel\n{{\n\n";
        public const string classEnd = "\n}\n";
        public const string EmptyFun = "\tprivate void {0}()\n\t{{\n\n\t}}\n\n";
        public const string FunStart = "\tprivate void {0}()\n\t{{\n";
        public const string FunOverrideStart = "\tpublic override void {0}()\n\t{{\n";
        public const string FunEnd = "\n\t}\n\n";
        public const string Lamda = "()=>\n\t\t{{\n\n\t\t}}";
        public const string Lamda1 = "({0})=>\n\t\t{{\n\n\t\t}}";
        public const string Lamda2 = "({0},{1})=>\n\t\t{{\n\n\t\t}}";

        public static string AddCallFun(string caller,string funName, string args = "")
        {
            return string.Format("\t\t{0}.{1}({2});\n", caller, funName, args);
        }

        public static string AddEmptyFun(string funName, string args = "")
        {
            return string.Format("\tprivate void {0}({1})\n\t{{\n\n\t}}\n\n", funName, args);
        }
    }
}