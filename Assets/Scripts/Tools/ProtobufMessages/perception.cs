// <auto-generated>
//   This file was generated by a tool; you should avoid making direct changes.
//   Consider using 'partial classes' to extend these types
//   Input: perception.proto
// </auto-generated>

#region Designer generated code
#pragma warning disable CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
namespace cloisim.msgs
{

    [global::ProtoBuf.ProtoContract()]
    public partial class Perception : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"tracking_id", IsRequired = true)]
        public int TrackingId { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"class_id", IsRequired = true)]
        public int ClassId { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"position", IsRequired = true)]
        public Vector3d Position { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"velocity", IsRequired = true)]
        public Vector3d Velocity { get; set; }

        [global::ProtoBuf.ProtoMember(5, Name = @"foot_prints")]
        public global::System.Collections.Generic.List<Vector3d> FootPrints { get; } = new global::System.Collections.Generic.List<Vector3d>();

    }

}

#pragma warning restore CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
#endregion
