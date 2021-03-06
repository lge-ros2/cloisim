// <auto-generated>
//   This file was generated by a tool; you should avoid making direct changes.
//   Consider using 'partial classes' to extend these types
//   Input: any.proto
// </auto-generated>

#region Designer generated code
#pragma warning disable CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
namespace cloisim.msgs
{

    [global::ProtoBuf.ProtoContract()]
    public partial class Any : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"type", IsRequired = true)]
        public ValueType Type { get; set; } = ValueType.None;

        [global::ProtoBuf.ProtoMember(2, Name = @"double_value")]
        public double DoubleValue
        {
            get => __pbn__DoubleValue.GetValueOrDefault();
            set => __pbn__DoubleValue = value;
        }
        public bool ShouldSerializeDoubleValue() => __pbn__DoubleValue != null;
        public void ResetDoubleValue() => __pbn__DoubleValue = null;
        private double? __pbn__DoubleValue;

        [global::ProtoBuf.ProtoMember(3, Name = @"int_value")]
        public int IntValue
        {
            get => __pbn__IntValue.GetValueOrDefault();
            set => __pbn__IntValue = value;
        }
        public bool ShouldSerializeIntValue() => __pbn__IntValue != null;
        public void ResetIntValue() => __pbn__IntValue = null;
        private int? __pbn__IntValue;

        [global::ProtoBuf.ProtoMember(4, Name = @"string_value")]
        [global::System.ComponentModel.DefaultValue("")]
        public string StringValue
        {
            get => __pbn__StringValue ?? "";
            set => __pbn__StringValue = value;
        }
        public bool ShouldSerializeStringValue() => __pbn__StringValue != null;
        public void ResetStringValue() => __pbn__StringValue = null;
        private string __pbn__StringValue;

        [global::ProtoBuf.ProtoMember(5, Name = @"bool_value")]
        public bool BoolValue
        {
            get => __pbn__BoolValue.GetValueOrDefault();
            set => __pbn__BoolValue = value;
        }
        public bool ShouldSerializeBoolValue() => __pbn__BoolValue != null;
        public void ResetBoolValue() => __pbn__BoolValue = null;
        private bool? __pbn__BoolValue;

        [global::ProtoBuf.ProtoMember(6, Name = @"vector3d_value")]
        public Vector3d Vector3dValue { get; set; }

        [global::ProtoBuf.ProtoMember(7, Name = @"color_value")]
        public Color ColorValue { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"pose3d_value")]
        public Pose Pose3dValue { get; set; }

        [global::ProtoBuf.ProtoMember(9, Name = @"quaternion_value")]
        public Quaternion QuaternionValue { get; set; }

        [global::ProtoBuf.ProtoMember(10, Name = @"time_value")]
        public Time TimeValue { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum ValueType
        {
            [global::ProtoBuf.ProtoEnum(Name = @"NONE")]
            None = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"DOUBLE")]
            Double = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"INT32")]
            Int32 = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"STRING")]
            String = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"BOOLEAN")]
            Boolean = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"VECTOR3D")]
            Vector3d = 6,
            [global::ProtoBuf.ProtoEnum(Name = @"COLOR")]
            Color = 7,
            [global::ProtoBuf.ProtoEnum(Name = @"POSE3D")]
            Pose3d = 8,
            [global::ProtoBuf.ProtoEnum(Name = @"QUATERNIOND")]
            Quaterniond = 9,
            [global::ProtoBuf.ProtoEnum(Name = @"TIME")]
            Time = 10,
        }

    }

}

#pragma warning restore CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
#endregion
