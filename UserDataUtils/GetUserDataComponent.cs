using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Runtime;
using Grasshopper.Kernel.Types;
using Grasshopper;
using Grasshopper.Kernel.Data;

namespace UserDataUtils
{
    public class GetUserDataComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public GetUserDataComponent()
          : base("Get User Data", "GUD",
              "Gets the user data attached to an object, if any.",
              "Speckle", "User Data Utils")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Object", "O", "Object to expand user dictionary of.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("User Dictionary", "D", "User Dictionary", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object o = null;
            DA.GetData(0, ref o);
            if (o == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to get object"); return; }

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

            Point pt = o as Rhino.Geometry.Point;
            if (pt != null)
                myObj = pt;

            if (myObj == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to get object");
                DA.SetData(0, null);
                return;
            }

            if (myObj.UserDictionary == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Object has no user dictionary.");
                DA.SetData(0, null);
                return;
            }

            DA.SetData(0, myObj.UserDictionary);
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
            get { return new Guid("{48f08adc-0fdd-41de-a038-a610e310cad9}"); }
        }
    }
}