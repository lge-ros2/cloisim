// <auto-generated>
//   This file was generated by a tool; you should avoid making direct changes.
//   Consider using 'partial classes' to extend these types
//   Input: micom.proto
// </auto-generated>

#region Designer generated code
#pragma warning disable CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
namespace cloisim.msgs
{

    [global::ProtoBuf.ProtoContract()]
    public partial class Micom : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"time", IsRequired = true)]
        public Time Time { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"odom")]
        public Odometry Odom { get; set; }

        [global::ProtoBuf.ProtoMember(3)]
        public Uss uss { get; set; }

        [global::ProtoBuf.ProtoMember(4)]
        public Ir ir { get; set; }

        [global::ProtoBuf.ProtoMember(5)]
        public Magnet magnet { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"imu")]
        public Imu Imu { get; set; }

        [global::ProtoBuf.ProtoMember(7)]
        public Bumper bumper { get; set; }

        [global::ProtoBuf.ProtoMember(8, Name = @"battery")]
        public Battery Battery { get; set; }

        [global::ProtoBuf.ProtoMember(9, Name = @"pose")]
        public Pose Pose { get; set; }

        [global::ProtoBuf.ProtoContract(Name = @"USS")]
        public partial class Uss : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"distance")]
            public double[] Distances { get; set; }

        }

        [global::ProtoBuf.ProtoContract(Name = @"IR")]
        public partial class Ir : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"distance")]
            public double[] Distances { get; set; }

        }

        [global::ProtoBuf.ProtoContract()]
        public partial class Odometry : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"angular_velocity", IsRequired = true)]
            public Wheel AngularVelocity { get; set; }

            [global::ProtoBuf.ProtoMember(2, Name = @"linear_velocity")]
            public Wheel LinearVelocity { get; set; }

            [global::ProtoBuf.ProtoMember(3, Name = @"pose")]
            public Vector3d Pose { get; set; }

            [global::ProtoBuf.ProtoMember(4, Name = @"twist_linear")]
            public Vector3d TwistLinear { get; set; }

            [global::ProtoBuf.ProtoMember(5, Name = @"twist_angular")]
            public Vector3d TwistAngular { get; set; }

            [global::ProtoBuf.ProtoContract()]
            public partial class Wheel : global::ProtoBuf.IExtensible
            {
                private global::ProtoBuf.IExtension __pbn__extensionData;
                global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                    => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

                [global::ProtoBuf.ProtoMember(1, Name = @"left", IsRequired = true)]
                public double Left { get; set; }

                [global::ProtoBuf.ProtoMember(2, Name = @"right", IsRequired = true)]
                public double Right { get; set; }

            }

        }

        [global::ProtoBuf.ProtoContract()]
        public partial class Magnet : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"detected")]
            public bool[] Detecteds { get; set; }

        }

        [global::ProtoBuf.ProtoContract()]
        public partial class Bumper : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"bumped")]
            public bool[] Bumpeds { get; set; }

        }

    }

}

#pragma warning restore CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
#endregion
