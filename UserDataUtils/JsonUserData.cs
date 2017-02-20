using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Newtonsoft.Json;
using Rhino.Collections;
using Grasshopper.Kernel.Types;
using System.Windows.Forms;
using System.IO;
using Grasshopper.Kernel.Parameters;

namespace UserDataUtils
{
    public class JsonUserData : GH_Component
    {
        string serialisedUDs;
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public JsonUserData()
          : base("User Data to JSON", "JUD",
              "Spits out a JSON string for the provided dictionary.",
              "Speckle", "User Data Utils")
        {
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            GH_DocumentObject.Menu_AppendSeparator(menu);
            GH_DocumentObject.Menu_AppendItem(menu, @"Save results to file.", (e, sender) =>
            {
                SaveFileDialog savefile = new SaveFileDialog();
                savefile.FileName = "userDictionaries.json";
                savefile.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";

                if (savefile.ShowDialog() == DialogResult.OK)
                {
                    using (StreamWriter sw = new StreamWriter(savefile.FileName))
                        sw.WriteLine(serialisedUDs);
                }
            });
            GH_DocumentObject.Menu_AppendSeparator(menu);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("User Data", "D", "User Dictionaries to export to JSON.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Format Data", "F", "Set to true to format data.", GH_ParamAccess.item, 0);
            Param_Integer param = pManager[1] as Param_Integer;
            param.AddNamedValue("Human readable string", 1);
            param.AddNamedValue("Non formatted string", 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("JSON", "S", "JSON output", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            int format=0;
            DA.GetData(1, ref format);

            List<object> objs = new List<object>();
            DA.GetDataList(0, objs);
            if (objs.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No dictionaries found.");
                return;
            }

            List<ArchivableDictionary> dictList = new List<ArchivableDictionary>();

            foreach (var obj in objs)
            {
                GH_ObjectWrapper goo = obj as GH_ObjectWrapper;
                if (goo == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provided object not a dictionary.");
                    return;
                }
                ArchivableDictionary dict = goo.Value as ArchivableDictionary;
                if (dict == null)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Provided object not a dictionary.");
                    return;
                }

                dictList.Add(dict);
            }
            if (dictList.Count == 0)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No dictionaries found.");
                return;
            }

            serialisedUDs = JsonConvert.SerializeObject(dictList, format==1 ? Formatting.Indented : Formatting.None);
            DA.SetData(0, serialisedUDs);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{f954660c-2c7d-4c2e-ab29-582e41d6c8da}"); }
        }
    }
}