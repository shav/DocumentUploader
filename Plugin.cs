using System.ComponentModel.Composition;

namespace Sungero.RxCmd
{
  [Export(typeof(IRxCmdPlugin))]
  public class Plugin : IRxCmdPlugin
  {
    public BaseCommand GetCommand()
    {
      return new DocumentsCommand("documents", "Import documents.");
    }
  }
}
