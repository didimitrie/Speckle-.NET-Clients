using Rhino;
using System;
using System.Windows.Forms;

namespace SpeckleRhino
{
    [System.Runtime.InteropServices.Guid("95B1D325-9CEA-480D-830F-36ECD25CBD4A")]
    public partial class SpeckleRhinoUserControl : UserControl
    {
        public SpeckleRhinoUserControl()
        {
            InitializeComponent();
            // Set the user control property on our plug-in
            SpeckleRhinoPlugIn.Instance.PanelUserControl = this;

            // Create a visible changed event handler
            VisibleChanged += OnVisibleChanged;

            // Create a dispose event handler
            Disposed += OnUserControlDisposed;
        }

        void OnVisibleChanged(object sender, EventArgs e)
        {
            // TODO...
            if (Visible) {

                
                
            }
        }

        /// <summary>
        /// Occurs when the component is disposed by a call to the
        /// System.ComponentModel.Component.Dispose() method.
        /// </summary>
        void OnUserControlDisposed(object sender, EventArgs e)
        {
            // Clear the user control property on our plug-in
            SpeckleRhinoPlugIn.Instance.PanelUserControl = null;
        }

        /// <summary>
        /// Returns the ID of this panel.
        /// </summary>
        public static Guid PanelId
        {
            get
            {
                return typeof(SpeckleRhinoUserControl).GUID;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
            if (!string.IsNullOrEmpty(textBox1.Text))
            {
                var rec = new SpeckleRhinoReceiver(textBox1.Text);
            }
            
        }
    }
}
