
namespace Peeper.Logic.Util
{
    [System.AttributeUsage(System.AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    sealed class EvalFileAttribute : System.Attribute
    {
        public string EvalFile { get; }
        public EvalFileAttribute(string evalFile)
        {
            this.EvalFile = evalFile;
        }
    }
}
