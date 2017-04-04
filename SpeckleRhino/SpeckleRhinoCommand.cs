using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.UI;

namespace SpeckleRhino
{
    [System.Runtime.InteropServices.Guid("89242b03-9a1a-4a28-95ed-cd4d620198e6")]
    public class SpeckleRhinoCommand : Command
    {
        public SpeckleRhinoCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static SpeckleRhinoCommand Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "SpeckleRhinoCommand"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var panel_id = SpeckleRhinoUserControl.PanelId;
            var visible = Panels.IsPanelVisible(panel_id);

            var prompt = visible
              ? "Speckle panel is visible. New value"
              : "Speckle panel is hidden. New value";

            var go = new GetOption();
            go.SetCommandPrompt(prompt);
            var hide_index = go.AddOption("Hide");
            var show_index = go.AddOption("Show");
            var toggle_index = go.AddOption("Toggle");

            go.Get();
            if (go.CommandResult() != Result.Success)
                return go.CommandResult();

            var option = go.Option();
            if (null == option)
                return Result.Failure;

            var index = option.Index;

            if (index == hide_index)
            {
                if (visible)
                    Panels.ClosePanel(panel_id);
            }
            else if (index == show_index)
            {
                if (!visible)
                    Panels.OpenPanel(panel_id);
            }
            else if (index == toggle_index)
            {
                if (visible)
                    Panels.ClosePanel(panel_id);
                else
                    Panels.OpenPanel(panel_id);
            }

            return Result.Success;
        }
    }
}
