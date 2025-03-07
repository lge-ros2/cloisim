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

        [global::ProtoBuf.ProtoMember(3, Name = @"imu")]
        public Imu Imu { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"uss")]
        public global::System.Collections.Generic.List<Uss> Usses { get; } = new global::System.Collections.Generic.List<Uss>();

        [global::ProtoBuf.ProtoMember(5, Name = @"ir")]
        public global::System.Collections.Generic.List<Ir> Irs { get; } = new global::System.Collections.Generic.List<Ir>();

        [global::ProtoBuf.ProtoMember(6, Name = @"magnet")]
        public global::System.Collections.Generic.List<Magnet> Magnets { get; } = new global::System.Collections.Generic.List<Magnet>();

        [global::ProtoBuf.ProtoMember(7, Name = @"bumper")]
        public global::System.Collections.Generic.List<Bumper> Bumpers { get; } = new global::System.Collections.Generic.List<Bumper>();

        [global::ProtoBuf.ProtoMember(8, Name = @"battery")]
        public Battery Battery { get; set; }

        [global::ProtoBuf.ProtoMember(9, Name = @"pose")]
        public Pose Pose { get; set; }

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

            [global::ProtoBuf.ProtoMember(4, Name = @"twist")]
            public Twist Twist { get; set; }

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

        [global::ProtoBuf.ProtoContract(Name = @"USS")]
        public partial class Uss : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"distance", IsRequired = true)]
            public double Distance { get; set; }

            [global::ProtoBuf.ProtoMember(2, Name = @"state")]
            public Sonar State { get; set; }

        }

        [global::ProtoBuf.ProtoContract(Name = @"IR")]
        public partial class Ir : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"distance", IsRequired = true)]
            public double Distance { get; set; }

            [global::ProtoBuf.ProtoMember(2, Name = @"state")]
            public Sonar State { get; set; }

        }

        [global::ProtoBuf.ProtoContract()]
        public partial class Magnet : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"detected", IsRequired = true)]
            public bool Detected { get; set; }

        }

        [global::ProtoBuf.ProtoContract()]
        public partial class Bumper : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"bumped", IsRequired = true)]
            public bool Bumped { get; set; }

            [global::ProtoBuf.ProtoMember(2, Name = @"contacts")]
            public Contacts Contacts { get; set; }

        }

    }

}

#pragma warning restore CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
#endregion
