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
              "Gets User Data",
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
            pManager.AddGenericParameter("Keys", "K", "UserDictionary Keys", GH_ParamAccess.list);
            pManager.AddGenericParameter("Values", "V", "UserDictionary Values", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object o = null;
            DA.GetData(0, ref o);
            if(o == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to get object"); return; }

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

            if(myObj == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to get object"); return; }

            if(myObj.UserDictionary == null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Object has no user dictionary."); return; }

            //
            var myList = new List<object>();
            foreach( var key in myObj.UserDictionary.Keys)
            {
                myList.Add(myObj.UserDictionary[key]);
            }

            DA.SetDataList(0, myObj.UserDictionary.Keys);
            DA.SetDataList(1, myList);
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