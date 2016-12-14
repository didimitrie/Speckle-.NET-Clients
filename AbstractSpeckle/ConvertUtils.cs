using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleAbstract
{
    /// <summary>
    /// Standardises object conversion. The SpeckleApiProxy requires a converter object which should derive from this interface.
    /// </summary>
    public interface SpeckleConverter
    {
        /// <summary>
        /// Converts a list of objects. 
        /// </summary>
        /// <param name="objects">Objects to convert.</param>
        /// <param name="getEncoded">If true, should return a base64 encoded version of the object as well.</param>
        /// <returns>A list of dynamic objects.</returns>
        List<dynamic> convert(IEnumerable<object> objects, bool getEncoded = false);
        /// <summary>
        /// Async conversion of a list of objects. Unblocks the main thread if calling straight from gh.
        /// </summary>
        /// <param name="objects">Objects to convert.</param>
        /// <param name="getEncoded">>If true, should return a base64 encoded version of the object as well.</param>
        /// <returns>A list of dynamic objects.</returns>
        Task<List<dynamic>> convertAsync(IEnumerable<object> objects, bool getEncoded = false);
        /// <summary>
        /// Spits out a string descriptor, ie "grasshopper-converter", or "rhino-converter". Used for reiniatisation of the API Proxy.
        /// </summary>
        /// <returns></returns>
        string serialise();
    }

    /// <summary>
    /// Handles Grasshopper objects: GH_Brep, GH_Mesh, etc. All non-gh specific converting functions are found in the RhinoGeometryConverter class. 
    /// </summary>
    public class GhConveter : SpeckleConverter
    {
        HashSet<string> sent = new HashSet<string>();

        List<dynamic> SpeckleConverter.convert(IEnumerable<object> objects, bool getEncoded = false)
        {
            List<dynamic> convertedObjects = new List<dynamic>();
            foreach (object o in objects)
            {
                var myObj = fromGhObject(o, true);
                var added = sent.Add((string)myObj.hash);
                if(added) convertedObjects.Add(fromGhObject(o, getEncoded));
            }

            return convertedObjects;
        }

        async Task<List<dynamic>> SpeckleConverter.convertAsync(IEnumerable<object> objects, bool getEncoded = false)
        {
            return await Task.Run(() =>
            {
                List<dynamic> convertedObjects = new List<dynamic>();
                foreach (object o in objects)
                {
                    var myObj = fromGhObject(o, true);
                    var added = sent.Add((string)myObj.hash);
                    if (added) convertedObjects.Add(fromGhObject(o, getEncoded));
                }

                return convertedObjects;
            });
        }

        private static dynamic fromGhObject(object o, bool getEncoded = false)
        {
            // baseStuff
            GH_Number num = o as GH_Number;
            if (num != null)
                return StandardTypesConverter.fromNumber(num.Value);

            GH_Boolean bul = o as GH_Boolean;
            if (bul != null)
                return StandardTypesConverter.fromBoolean(bul.Value);

            GH_String str = o as GH_String;
            if (str != null)
                return StandardTypesConverter.fromString(str.Value);

            // geometry
            GH_Point point = o as GH_Point;
            if (point != null)
                return RhinoGeometryConverter.fromPoint(point.Value);

            GH_Vector vector = o as GH_Vector;
            if (vector != null)
                return RhinoGeometryConverter.fromVector(vector.Value);

            GH_Line line = o as GH_Line;
            if (line != null)
                return RhinoGeometryConverter.fromLine(line.Value);

            GH_Surface surface = o as GH_Surface;
            if (surface != null)
                return RhinoGeometryConverter.fromBrep(surface.Value, getEncoded);

            GH_Brep brep = o as GH_Brep;
            if (brep != null)
                return RhinoGeometryConverter.fromBrep(brep.Value, getEncoded);

            GH_Mesh mesh = o as GH_Mesh;
            if (mesh != null)
                return RhinoGeometryConverter.fromMesh(mesh.Value, false); // meshes to meshes, ashes to ashes, dust to dust

            return new { type = "404", hash = "404", value = "type not supported" };
        }

        public static dynamic formInterval(GH_Interval interval)
        {
            // TODO
            return new { };
        }

        public static dynamic fromInterval2d(GH_Interval2D interval)
        {
            // TODO
            return new { };
        }

        public string serialise()
        {
            return "grasshopper-converter";
        }

    }


    /// <summary>
    /// Handles Rhino objects. Probably should use this one inside a scripting component (at least for geometry).
    /// I hate typecasting. It's a well mess. At least in Grasshopper.
    /// </summary>
    public class RhConverter : SpeckleConverter
    {
        public List<dynamic> convert(IEnumerable<object> objects, bool getEncoded = false)
        {
            throw new NotImplementedException();
        }

        public Task<List<dynamic>> convertAsync(IEnumerable<object> objects, bool getEncoded = false)
        {
            throw new NotImplementedException();
        }

        public string serialise()
        {
            return "rhino-converter";
        }
    }

    /// <summary>
    /// Handles conversion from base rhino geometry types. 
    /// </summary>
    static class RhinoGeometryConverter
    {
        public static dynamic fromPoint(Point3d o)
        {
            return new
            {
                type = "Point",
                hash = "Point." + o.X + "." + o.Y + "." + o.Z,
                value = new { x = o.X, y = o.Y, z = o.Z }
            };
        }

        public static dynamic fromVector(Vector3d v)
        {
            return new
            {
                type = "Vector",
                hash = "Vector." + v.X + "." + v.Y + "." + v.Z,
                value = new { x = v.X, y = v.Y, z = v.Z }
            };
        }

        public static dynamic fromLine(Line o)
        {
            return new
            {
                type = "Line",
                hash = "Line." + ConvertUtils.getHash(ConvertUtils.getBase64(o)),
                value = new
                {
                    start = fromPoint(o.From),
                    end = fromPoint(o.To)
                },
                properties = new
                {
                    lenght = o.Length
                }
            };
        }


        public static dynamic fromMesh(Mesh o, bool getEncoded)
        {
            var encodedObj = ConvertUtils.getBase64(o);

            return new
            {
                type = "Mesh",
                hash = "Mesh." + ConvertUtils.getHash(encodedObj),
                encodedValue = getEncoded ? encodedObj : "",
                value = new
                {
                    vertices = o.Vertices,
                    faces = o.Faces,
                    colors = o.VertexColors
                }
            };
        }

        public static dynamic fromBrep(Brep o, bool getEncoded)
        {
            var encodedObj = ConvertUtils.getBase64(o);
            var ms = getMeshFromBrep(o);
            return new
            {
                type = "Brep",
                hash = "Brep." + ConvertUtils.getHash(encodedObj),
                encodedValue = getEncoded ? encodedObj : "",
                value = new
                {
                    vertices = ms.Vertices,
                    faces = ms.Faces,
                    colors = ms.VertexColors
                }
            };
        }

        public static Mesh getMeshFromBrep(Brep b)
        {
            Mesh[] meshes = Mesh.CreateFromBrep(b);
            Mesh joinedMesh = new Mesh();
            foreach (Mesh m in meshes) joinedMesh.Append(m);
            return joinedMesh;
        }
    }

    static class StandardTypesConverter
    {
        public static dynamic fromBoolean(bool b)
        {
            return new
            {
                type = "Boolean",
                hash = "Boolean." + b.ToString(),
                value = b ? 1 : 0
            };
        }

        public static dynamic fromNumber(double num)
        {
            return new
            {
                type = "Number",
                hash = "Number." + num,
                value = num
            };
        }

        public static dynamic fromString(string str)
        {
            return new
            {
                type = "String",
                hash = "String." + str,
                value = str
            };
        }
    }

    static class ConvertUtils
    {
        public static string getBase64(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Serialize(ms, obj);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public static byte[] getBytes(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public static string getHash(string str)
        {
            byte[] hash;
            using (MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(str));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash)
                    sb.Append(b.ToString("X2"));

                return sb.ToString().ToLower();
            }
        }
    }
}
