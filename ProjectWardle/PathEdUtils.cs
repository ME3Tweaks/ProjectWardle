using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrossGenV.Classes
{
    /// <summary>
    /// Stripped down version from LEX
    /// </summary>
    public class PathEdUtils
    {
        public static Point3D GetLocation(ExportEntry export)
        {
            float x = 0, y = 0, z = int.MinValue;
            if (export.ClassName.Contains("Component") && export.HasParent && export.Parent.ClassName.Contains("CollectionActor"))  //Collection component
            {
                var actorCollection = export.Parent as ExportEntry;
                var collection = GetCollectionItems(actorCollection);

                if (!(collection?.IsEmpty() ?? true))
                {
                    var positions = GetCollectionLocationData(actorCollection);
                    var idx = collection.FindIndex(o => o != null && o.UIndex == export.UIndex);
                    if (idx >= 0)
                    {
                        return new Point3D(positions[idx].X, positions[idx].Y, positions[idx].Z);
                    }
                }

            }
            else
            {
                var prop = export.GetProperty<StructProperty>("location");
                if (prop != null)
                {
                    foreach (var locprop in prop.Properties)
                    {
                        switch (locprop)
                        {
                            case FloatProperty fltProp when fltProp.Name == "X":
                                x = fltProp;
                                break;
                            case FloatProperty fltProp when fltProp.Name == "Y":
                                y = fltProp;
                                break;
                            case FloatProperty fltProp when fltProp.Name == "Z":
                                z = fltProp;
                                break;
                        }
                    }
                    return new Point3D(x, y, z);
                }
            }
            return new Point3D(0, 0, 0);
        }

        public static List<Point3D> GetCollectionLocationData(ExportEntry collectionactor)
        {
            if (!collectionactor.ClassName.Contains("CollectionActor"))
                return null;

            return ((StaticCollectionActor)ObjectBinary.From(collectionactor)).LocalToWorldTransforms
                                                                              .Select(localToWorldTransform => (Point3D)localToWorldTransform.Translation).ToList();
        }

        public static List<ExportEntry> GetCollectionItems(ExportEntry smac)
        {
            var collectionItems = new List<ExportEntry>();
            var smacItems = smac.GetProperty<ArrayProperty<ObjectProperty>>(smac.ClassName == "StaticMeshCollectionActor" ? "StaticMeshComponents" : "LightComponents");
            if (smacItems != null)
            {
                //Read exports...
                foreach (ObjectProperty obj in smacItems)
                {
                    if (obj.Value > 0)
                    {
                        ExportEntry item = smac.FileRef.GetUExport(obj.Value);
                        collectionItems.Add(item);
                    }
                    else
                    {
                        //this is a blank entry, or an import, somehow.
                        collectionItems.Add(null);
                    }
                }
                return collectionItems;
            }
            return null;
        }

        public static void SetDrawScale3D(ExportEntry export, float x, float y, float z)
        {
            if (export.ClassName.Contains("Component"))
            {
                SetCollectionActorDrawScale3D(export, x, y, z);
            }
            else
            {
                export.WriteProperty(CommonStructs.Vector3Prop(x, y, z, "DrawScale3D"));
            }
        }

        public static void SetLocation(ExportEntry export, float x, float y, float z)
        {
            if (export.ClassName.Contains("Component"))
            {
                SetCollectionActorLocation(export, x, y, z);
            }
            else
            {
                export.WriteProperty(CommonStructs.Vector3Prop(x, y, z, "location"));
            }
        }

        public static void SetLocation(ExportEntry export, Point3D point)
        {
            if (export.ClassName.Contains("Component"))
            {
                SetCollectionActorLocation(export, point.X, point.Y, point.Z);
            }
            else
            {
                export.WriteProperty(CommonStructs.Vector3Prop(point.X, point.Y, point.Z, "location"));
            }
        }

        public static void SetLocation(StructProperty prop, float x, float y, float z)
        {
            prop.GetProp<FloatProperty>("X").Value = x;
            prop.GetProp<FloatProperty>("Y").Value = y;
            prop.GetProp<FloatProperty>("Z").Value = z;
        }

        public static void SetCollectionActorLocation(ExportEntry component, float x, float y, float z, List<ExportEntry> collectionitems = null, ExportEntry collectionactor = null)
        {
            if (collectionactor == null)
            {
                if (!(component.HasParent && component.Parent.ClassName.Contains("CollectionActor")))
                    return;
                collectionactor = (ExportEntry)component.Parent;
            }

            collectionitems ??= GetCollectionItems(collectionactor);

            if (collectionitems?.Count > 0)
            {
                var idx = collectionitems.FindIndex(o => o != null && o.UIndex == component.UIndex);
                if (idx >= 0)
                {
                    var binData = (StaticCollectionActor)ObjectBinary.From(collectionactor);

                    Matrix4x4 m = binData.LocalToWorldTransforms[idx];
                    m.Translation = new Vector3(x, y, z);
                    binData.LocalToWorldTransforms[idx] = m;

                    collectionactor.WriteBinary(binData);
                }
            }
        }

        public static void SetCollectionActorDrawScale3D(ExportEntry component, float x, float y, float z, List<ExportEntry> collectionitems = null, ExportEntry collectionactor = null)
        {
            if (collectionactor == null)
            {
                if (!(component.HasParent && component.Parent.ClassName.Contains("CollectionActor")))
                    return;
                collectionactor = (ExportEntry)component.Parent;
            }

            collectionitems ??= GetCollectionItems(collectionactor);

            if (collectionitems?.Count > 0)
            {
                var idx = collectionitems.FindIndex(o => o != null && o.UIndex == component.UIndex);
                if (idx >= 0)
                {
                    var binData = (StaticCollectionActor)ObjectBinary.From(collectionactor);
                    Matrix4x4 m = binData.LocalToWorldTransforms[idx];
                    var dsd = m.UnrealDecompose();
                    binData.LocalToWorldTransforms[idx] = ActorUtils.ComposeLocalToWorld(dsd.translation, dsd.rotation, new Vector3(x, y, z));
                    collectionactor.WriteBinary(binData);
                }
            }
        }
    }

    public class Point3D
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Point3D()
        {

        }

        public Point3D(float X, float Y, float Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public double getDistanceToOtherPoint(Point3D other)
        {
            double deltaX = X - other.X;
            double deltaY = Y - other.Y;
            double deltaZ = Z - other.Z;

            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
        }

        public Point3D getDelta(Point3D other)
        {
            float deltaX = X - other.X;
            float deltaY = Y - other.Y;
            float deltaZ = Z - other.Z;
            return new Point3D(deltaX, deltaY, deltaZ);
        }

        public override string ToString()
        {
            return $"{X},{Y},{Z}";
        }

        public Point3D applyDelta(Point3D other)
        {
            float deltaX = X + other.X;
            float deltaY = Y + other.Y;
            float deltaZ = Z + other.Z;
            return new Point3D(deltaX, deltaY, deltaZ);
        }

        public static implicit operator Point3D(Vector3 vec) => new Point3D(vec.X, vec.Y, vec.Z);
    }
}
