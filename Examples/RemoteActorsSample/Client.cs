﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using Stacks.Actors;

namespace RemoteActorsSample
{
     [ProtoContract]
    public class TriangleInfo
    {
        [ProtoMember(1)]
        public double Field { get; set; }
        [ProtoMember(2)]
        public double Height { get; set; }
    }

    [ProtoContract]
    public class Rectangle
    {
        [ProtoMember(1)]
        public double A { get; set; }
        [ProtoMember(2)]
        public double B { get; set; }
    }

    [ProtoContract]
    public class RectangleInfo
    {
        [ProtoMember(1)]
        public double Field { get; set; }
        [ProtoMember(2)]
        public double Perimeter { get; set; }
    }

    [ProtoContract]
    public class Triangle
    {
        [ProtoMember(1)]
        public double A { get; set; }
        [ProtoMember(2)]
        public double B { get; set; }
        [ProtoMember(3)]
        public double C { get; set; }
    }

    public interface ICalculatorActor2
    {
        Task<double> Multiply(double x, double y);
    }

    public interface ICalculatorActor : IActorClientProxy
    {
        Task<double> Add(double x, double y);
        Task<double> Subtract(double x, double y);
        Task<double> Increment(double x);

        Task<RectangleInfo> GetRectData(Rectangle rect);
        Task<TriangleInfo> GetTriangleData(Triangle triangle);
        Task<TriangleInfo> GetTriangleData2(Triangle triangle, double f);

        Task PushInfo(double x);
        Task Ping();
    }
}
