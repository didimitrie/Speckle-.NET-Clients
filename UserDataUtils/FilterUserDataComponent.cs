using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Runtime;
using Grasshopper.Kernel.Types;
using System.Linq;

namespace UserDataUtils
{
    public class FilterUserDataComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public FilterUserDataComponent()
          : base("Filter User Data", "FUD",
              "Filters User Data by Property name",
              "Speckle", "User Data Utils")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Object", "O", "Object to set user data to.", GH_ParamAccess.item);
            pManager.AddTextParameter("Propery Key", "K", "Property key that you are looking for.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Object", "O", "Objects that have the property specified.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Property Value", "V", "Specified property's value.", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object o = null;
            string key = null;

            DA.GetData(0, ref o);
            DA.GetData(1, ref key);

            if (o == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to get object"); return; }
            if (key == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to get key"); return; }

            CommonObject myObj = null;

            GH_Mesh mesh = o as GH_Mesh;
            if (mesh != null)
                myObj = mesh.Value;

            GH_Brep brep = o as GH_Brep;
            if (brep != null)
                myObj = brep.Value;

            GH_Surface srf = o as GH_Surface;
            if (srf != null)
                myObj = srf.Value;

            GH_Curve crv = o as GH_Curve;
            if (crv != null)
                myObj = crv.Value;

            if (myObj == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to get object"); return; }

            if (myObj.UserDictionary == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Object has no user dictionary."); return; }

            if(myObj.UserDictionary.Keys.Contains(key))
            {
                DA.SetData(0, o);
                DA.SetData(1, myObj.UserDictionary[key]);
            }
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
            get { return new Guid("{6a5b0d11-ecf9-4d17-be5b-08273d3db2c9}"); }
        }
    }
}