using Rhino.PlugIns;
using Rhino.UI;

namespace SpeckleRhino
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class SpeckleRhinoPlugIn : Rhino.PlugIns.PlugIn

    {
        public SpeckleRhinoPlugIn()
        {
            Instance = this;
        }

        ///<summary>Gets the only instance of the SpeckleRhinoPlugIn plug-in.</summary>
        public static SpeckleRhinoPlugIn Instance
        {
            get; private set;
        }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and mantain plug-in wide options in a document.

        /// <summary>
        /// The tabbed dockbar user control
        /// </summary>
        public SpeckleRhinoUserControl PanelUserControl { get; set; }

        public SpeckleRhinoAgent SpeckleAgent { get; set;}

        /// <summary>
        /// Called when the plug-in is being loaded.
        /// </summary>
        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            var panel_type = typeof(SpeckleRhinoUserControl);
            Panels.RegisterPanel(this, panel_type, "Speckle", Properties.Resources.Panel);
            return LoadReturnCode.Success;
        }
    }
}