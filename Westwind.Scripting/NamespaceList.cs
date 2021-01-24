using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    public class ReferenceList : HashSet<string>
    {

        /// <summary>
        /// Assign the references to the Compiler Parameters
        /// </summary>
        /// <param name="parameters">Parameter options</param>
        public void SetReferences(CompilerParameters parameters)
        {
            var refs = this.ToArray();
            if (refs.Length > 0)
                parameters.ReferencedAssemblies.AddRange(refs);
        }
    }
}
