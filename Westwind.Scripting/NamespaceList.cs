using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Westwind.Scripting
{
    /// <summary>
    /// HashSet of namespaces
    /// </summary>
    public class NamespaceList : HashSet<string>
    {
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            var enumerator = this.GetEnumerator();
            foreach (string ns in this)
            {
                sb.AppendLine($"using {ns};");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// HashSet of References
    /// </summary>
    public class ReferenceList : HashSet<PortableExecutableReference>
    {

    }
}
