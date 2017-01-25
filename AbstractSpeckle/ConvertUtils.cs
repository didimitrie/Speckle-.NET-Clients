using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using SpeckleClient;
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
    /// Handles Grasshopper objects: GH_Brep, GH_Mesh, etc. All non-gh specific converting functions are found in the RhinoGeometryConverter class. 
    /// </summary>
    public class GhRhConveter : SpeckleConverter
    {
        /// <summary>
        /// Keeps track of objects sent in this session, and sends only the ones unsent so far. 
        /// Reduces some server load and payload size, which makes for more world peace and love in general.
        /// </summary>
        HashSet<string> sent = new HashSet<string>();

        public GhRhConveter(bool _encodeObjectsToSpeckle, bool _encodeObjectsToNative) : base(_encodeObjectsToSpeckle, _encodeObjectsToNative)
        {
        }

        // sync method
        public override List<dynamic> convert(IEnumerable<object> objects)
        {
            List<dynamic> convertedObjects = new List<dynamic>();
            foreach (object o in objects)
            {
                var myObj = fromGhRhObject(o, encodeObjectsToNative, encodeObjectsToSpeckle);

                if (nonHashedTypes.Contains((string)myObj.type))
                    convertedObjects.Add(myObj);
                else
                {
                    var added = sent.Add((string)myObj.hash);
                    if (added)
                        convertedObjects.Add(myObj);
                    else
                        convertedObjects.Add(new { type = myObj.type, hash = myObj.hash });
                }
            }

            return convertedObjects;
        }

        // async callback
        public override void convertAsync(IEnumerable<object> objects, Action<List<dynamic>> callback)
        {
            callback(this.convert(objects));
        }

        // async task
        public override async Task<List<dynamic>> convertAsyncTask(IEnumerable<object> objects)
        {
            return await Task.Run(() =>
            {
                return convert(objects);
            });
        }

        public override object encodeObject(dynamic obj)
        {
            string type = (string)obj.type;
            switch(type)
            {
                case "Number":
                    return GhRhConveter.toNumber(obj);
                default:
                    return "404";
            };
        }

        /// <summary>
        /// Determines object type and calls the appropriate conversion call. 
        /// </summary>
        /// <param name="o">Object to convert.</param>
        /// <param name="getEncoded">If set to true, will return a base64 encoded value of more complex objects (ie, breps).</param>
        /// <returns></returns>
        private static dynamic fromGhRhObject(object o, bool getEncoded = false, bool getAbstract = true)
        {
            // grasshopper specific
            GH_Interval int1d = o as GH_Interval;

            GH_Interval2D int2d = o as GH_Interval2D;

            GH_Colour col = o as GH_Colour;

            // basic stuff
            GH_Number num = o as GH_Number;
            if (num != null)
                return SpeckleConverter.fromNumber(num.Value);

            GH_Boolean bul = o as GH_Boolean;
            if (bul != null)
                return SpeckleConverter.fromBoolean(bul.Value);

            GH_String str = o as GH_String;
            if (str != null)
                return SpeckleConverter.fromString(str.Value);

            // simple geometry
            GH_Point point = o as GH_Point;
            if (point != null)
                return GhRhConveter.fromPoint(point.Value);

            GH_Vector vector = o as GH_Vector;
            if (vector != null)
                return GhRhConveter.fromVector(vector.Value);

            GH_Plane plane = o as GH_Plane;
            if (plane != null)
                return GhRhConveter.fromPlane(plane.Value);

            GH_Line line = o as GH_Line;
            if (line != null)
                return GhRhConveter.fromLine(line.Value);

            // strange geometry stuff
            GH_Arc arc = o as GH_Arc;
            if (arc != null)
                return GhRhConveter.fromArc(arc.Value);

            GH_Circle circle = o as GH_Circle;
            if (circle != null)
                return GhRhConveter.fromCircle(circle.Value);

            GH_Rectangle rectangle = o as GH_Rectangle;
            if (rectangle != null)
                return GhRhConveter.fromRectangle(rectangle.Value);

            GH_Box box = o as GH_Box;
            if (box != null)
                return GhRhConveter.fromBox(box.Value);
            if (o is Box)
                return GhRhConveter.fromBox((Box) o);

            // getting complex 
            GH_Curve curve = o as GH_Curve;
            if (curve != null)
            {
                Polyline poly;
                if (curve.Value.TryGetPolyline(out poly))
                    return GhRhConveter.fromPolyline(poly);
                return GhRhConveter.fromCurve(curve.Value, getEncoded, getAbstract);
            }

            GH_Surface surface = o as GH_Surface;
            if (surface != null)
                return GhRhConveter.fromBrep(surface.Value, getEncoded, getAbstract);

            GH_Brep brep = o as GH_Brep;
            if (brep != null)
                return GhRhConveter.fromBrep(brep.Value, getEncoded, getAbstract);

            GH_Mesh mesh = o as GH_Mesh;
            if (mesh != null)
                return GhRhConveter.fromMesh(mesh.Value);

            // If we reached this place, means we don't know what we're doing...
            return new { type = "404", hash = "404", value = "type not supported" };
        }

        #region Special grasshopper kernel types (non-rhino geometry)

        public static dynamic formInterval(GH_Interval interval)
        {
            // TODO
            return new { };
        }

        public static GH_Interval toInterval(dynamic obj)
        {
            return null;
        }

        public static dynamic fromInterval2d(GH_Interval2D interval)
        {
            // TODO
            return new { };
        }

        public static GH_Interval2D toInterval2d(dynamic obj)
        {
            return null;
        }

        #endregion

        #region Rhino Geometry Converters

        public static dynamic fromPoint(Point3d o)
        {
            return new
            {
                type = "Point",
                hash = "Point." + "NoHash",
                value = new { x = o.X, y = o.Y, z = o.Z }
            };
        }

        public static Point3d toPoint(dynamic o)
        {
            // TODO
            return new Point3d();
        }

        public static dynamic fromVector(Vector3d v)
        {
            return new
            {
                type = "Vector",
                hash = "Vector." + "NoHash",
                value = new { x = v.X, y = v.Y, z = v.Z }
            };
        }

        public static Vector3d toVector(dynamic o)
        {
            // TODO
            return new Vector3d();
        }

        public static dynamic fromPlane(Plane p)
        {
            return new
            {
                type = "Plane",
                hash = "Plane." + SpeckleConverter.getHash("RH:::" + SpeckleConverter.getBase64(p)),
                value = new
                {
                    origin = fromPoint(p.Origin),
                    xdir = fromVector(p.XAxis),
                    ydir = fromVector(p.YAxis),
                    normal = fromVector(p.Normal)
                }
            };
        }

        public static dynamic fromLine(Line o)
        {
            return new
            {
                type = "Line",
                hash = "Line." + SpeckleConverter.getHash("RH:::" + SpeckleConverter.getBase64(o)),
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

        public static Line toLine(dynamic o)
        {
            return new Line();
        }

        public static dynamic fromPolyline(Polyline poly)
        {
            var encodedObj = "RH:::" + SpeckleConverter.getBase64(poly.ToNurbsCurve());
            Rhino.Geometry.AreaMassProperties areaProps = Rhino.Geometry.AreaMassProperties.Compute(poly.ToNurbsCurve());
            return new
            {
                type = "Polyline",
                hash = "Polyline." + SpeckleConverter.getHash(encodedObj),
                value = poly,
                properties = new
                {
                    length = poly.Length,
                    area = areaProps != null ? areaProps.Area : 0,
                    areaCentroid = areaProps != null ? fromPoint(areaProps.Centroid) : null
                }
            };
        }

        public static Polyline toPolyline(dynamic o)
        {
            return new Polyline();
        }

        public static dynamic fromCurve(Curve o, bool getEncoded, bool getAbstract)
        {
            var encodedObj = "RH:::" + SpeckleConverter.getBase64(o);
            Rhino.Geometry.AreaMassProperties areaProps = Rhino.Geometry.AreaMassProperties.Compute(o);
            var polyCurve = o.ToPolyline(0, 1, 0, 0, 0, 0.1, 0, 0, true);
            Polyline poly; polyCurve.TryGetPolyline(out poly);
            return new
            {
                type = "Curve",
                hash = "Curve." + SpeckleConverter.getHash(encodedObj),
                value = poly,
                encodedValue = getEncoded ? encodedObj : "",
                properties = new
                {
                    lenght = o.GetLength(),
                    area = areaProps != null ? areaProps.Area : 0,
                    areaCentroid = areaProps != null ? fromPoint(areaProps.Centroid) : null
                }
            };
        }

        public static Curve toCurve(dynamic o)
        {
            throw new NotImplementedException();
        }

        public static dynamic fromMesh(Mesh o)
        {
            var encodedObj = "RH:::" + SpeckleConverter.getBase64(o);
            Rhino.Geometry.VolumeMassProperties volumeProps = Rhino.Geometry.VolumeMassProperties.Compute(o);
            Rhino.Geometry.AreaMassProperties areaProps = Rhino.Geometry.AreaMassProperties.Compute(o);

            return new
            {
                type = "Mesh",
                hash = "Mesh." + SpeckleConverter.getHash(encodedObj),
                value = new
                {
                    vertices = o.Vertices,
                    faces = o.Faces,
                    colors = o.VertexColors
                },
                properties = new
                {
                    volume = volumeProps.Volume,
                    area = areaProps.Area,
                    volumeCentroid = fromPoint(volumeProps.Centroid),
                    areaCentroid = fromPoint(areaProps.Centroid)
                }
            };
        }

        public static Mesh toMesh(dynamic o)
        {
            throw new NotImplementedException();
        }

        public static dynamic fromBrep(Brep o, bool getEncoded, bool getAbstract)
        {
            var encodedObj = "RH:::" + SpeckleConverter.getBase64(o);
            var ms = getMeshFromBrep(o);

            Rhino.Geometry.VolumeMassProperties volumeProps = Rhino.Geometry.VolumeMassProperties.Compute(o);
            Rhino.Geometry.AreaMassProperties areaProps = Rhino.Geometry.AreaMassProperties.Compute(o);

            return new
            {
                type = "Brep",
                hash = "Brep." + SpeckleConverter.getHash(encodedObj),
                encodedValue = getEncoded ? encodedObj : "",
                value = new
                {
                    vertices = ms.Vertices,
                    faces = ms.Faces,
                    colors = ms.VertexColors
                },
                properties = new
                {
                    volume = volumeProps.Volume,
                    area = areaProps.Area,
                    volumeCentroid = fromPoint(volumeProps.Centroid),
                    areaCentroid = fromPoint(areaProps.Centroid)
                }
            };
        }

        public static Brep toBrep(dynamic o)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Little utility function
        /// </summary>
        /// <param name="b"></param>
        /// <returns>A joined mesh of all the brep's faces</returns>
        private static Mesh getMeshFromBrep(Brep b)
        {
            Mesh[] meshes = Mesh.CreateFromBrep(b);
            Mesh joinedMesh = new Mesh();
            foreach (Mesh m in meshes) joinedMesh.Append(m);
            return joinedMesh;
        }

        public static dynamic fromArc(Arc arc)
        {
            return new
            {
                type = "Arc",
                hash = "Arc." + SpeckleConverter.getHash("RH:::" + SpeckleConverter.getBase64(arc)),
                value = new
                {
                    center = fromPoint(arc.Center),
                    plane = fromPlane(arc.Plane),
                    startAngle = arc.StartAngle,
                    endAngle = arc.EndAngle
                }
            };
        }

        public static dynamic fromCircle(Circle circle)
        {
            return new
            {
                type = "Circle",
                hash = "Circle." + SpeckleConverter.getHash("RH:::" + SpeckleConverter.getBase64(circle)),
                value = new
                {
                    center = fromPoint(circle.Center),
                    normal = fromVector(circle.Plane.Normal),
                    radius = circle.Radius
                }
            };
        }

        public static dynamic fromRectangle(Rectangle3d rect)
        {
            return new
            {
                type = "Rectangle",
                hash = "Rectangle." + SpeckleConverter.getHash("RH:::" + SpeckleConverter.getBase64(rect)),
                value = new
                {
                    A = rect.Corner(0), // to use fromPoint()
                    B = rect.Corner(1),
                    C = rect.Corner(2),
                    D = rect.Corner(3),
                    plane = fromPlane(rect.Plane)
                }
            };
        }

        public static dynamic fromBox(Box box)
        {
            return new
            {
                type = "Box",
                hash = "Box." + SpeckleConverter.getHash("RH:::" + SpeckleConverter.getBase64(box)),
                value = new
                {
                    center = box.Center, // to use fromPoint()
                    normal = box.Plane.Normal, // use fromVector
                    plane = box.Plane, // to use fromPlane
                    X = box.X,
                    Y = box.Y,
                    Z = box.Z
                }
            };
        }

        #endregion

        #region last things

        public override dynamic description()
        {
            return new
            {
                type = "grasshopper",
                encodeObjectsToNative = encodeObjectsToNative,
                encodeObjectsToSpeckle = encodeObjectsToSpeckle
            };
        }

        #endregion
    }
}
