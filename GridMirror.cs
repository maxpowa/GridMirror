using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRageMath;

namespace GridMirror
{
    public enum MirrorAxis { X, Y, Z }

    public class MirrorResult
    {
        public int BlocksMirrored;
        public MyObjectBuilder_CubeGrid? MirroredGrid;

        public override string ToString()
        {
            return "Blocks mirrored: " + BlocksMirrored;
        }
    }

    public static class GridMirror
    {
        public static MirrorResult Mirror(MyObjectBuilder_CubeGrid gridBuilder, MirrorAxis axis)
        {
            var result = new MirrorResult();

            if (gridBuilder.CubeBlocks == null || gridBuilder.CubeBlocks.Count == 0)
                return result;

            var originalBlocks = new System.Collections.Generic.List<MyObjectBuilder_CubeBlock>(gridBuilder.CubeBlocks);

            var min = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);
            var max = new Vector3I(int.MinValue, int.MinValue, int.MinValue);

            foreach (var block in originalBlocks)
            {
                var pos = (Vector3I)block.Min;
                min = Vector3I.Min(min, pos);
                max = Vector3I.Max(max, pos);
            }

            var centerSum = min + max;

            var mirroredBlocks = new System.Collections.Generic.List<MyObjectBuilder_CubeBlock>();
            foreach (var block in originalBlocks)
            {
                var copy = (MyObjectBuilder_CubeBlock)block.Clone();
                copy.EntityId = 0;

                var defId = new MyDefinitionId(block.TypeId, block.SubtypeName);
                MyCubeBlockDefinition? def = null;
                MyDefinitionManager.Static?.TryGetCubeBlockDefinition(defId, out def);

                MirrorBlockPosition(copy, axis, centerSum, def!);
                MirrorBlockOrientation(copy, axis, def!);

                mirroredBlocks.Add(copy);
                result.BlocksMirrored++;
            }

            gridBuilder.CubeBlocks.Clear();
            gridBuilder.CubeBlocks.AddRange(mirroredBlocks);

            MyAPIGateway.Entities.RemapObjectBuilder(gridBuilder);

            result.MirroredGrid = gridBuilder;
            return result;
        }

        private static void MirrorBlockPosition(MyObjectBuilder_CubeBlock block, MirrorAxis axis, Vector3I centerSum, MyCubeBlockDefinition def)
        {
            var pos = (Vector3I)block.Min;

            if (def != null && (def.Size.X > 1 || def.Size.Y > 1 || def.Size.Z > 1))
            {
                var orientation = new MyBlockOrientation(block.BlockOrientation.Forward, block.BlockOrientation.Up);
                Matrix orientMatrix;
                orientation.GetMatrix(out orientMatrix);

                var sizeMinusOne = new Vector3(def.Size.X - 1, def.Size.Y - 1, def.Size.Z - 1);
                var orientedSize = Vector3.Abs(Vector3.TransformNormal(sizeMinusOne, orientMatrix));
                var extent = new Vector3I(
                    (int)System.Math.Round(orientedSize.X),
                    (int)System.Math.Round(orientedSize.Y),
                    (int)System.Math.Round(orientedSize.Z));

                var blockMax = pos + extent;

                var mirroredMin = MirrorVector(pos, axis, centerSum);
                var mirroredMax = MirrorVector(blockMax, axis, centerSum);

                block.Min = (VRage.SerializableVector3I)Vector3I.Min(mirroredMin, mirroredMax);
            }
            else
            {
                block.Min = (VRage.SerializableVector3I)MirrorVector(pos, axis, centerSum);
            }
        }

        private static Vector3I MirrorVector(Vector3I v, MirrorAxis axis, Vector3I centerSum)
        {
            switch (axis)
            {
                case MirrorAxis.X: return new Vector3I(centerSum.X - v.X, v.Y, v.Z);
                case MirrorAxis.Y: return new Vector3I(v.X, centerSum.Y - v.Y, v.Z);
                case MirrorAxis.Z: return new Vector3I(v.X, v.Y, centerSum.Z - v.Z);
                default: return v;
            }
        }

        private static void MirrorBlockOrientation(MyObjectBuilder_CubeBlock block, MirrorAxis axis, MyCubeBlockDefinition def)
        {
            if (def == null)
            {
                var fwd = SimpleMirrorDirection(block.BlockOrientation.Forward, axis);
                var up = SimpleMirrorDirection(block.BlockOrientation.Up, axis);
                block.BlockOrientation = new SerializableBlockOrientation(fwd, up);
                return;
            }

            MyCubeBlockDefinition mirrorDef = def;
            if (!string.IsNullOrEmpty(def.MirroringBlock))
            {
                var mirrorDefId = new MyDefinitionId(def.Id.TypeId, def.MirroringBlock);
                MyCubeBlockDefinition? tempDef = null;
                if (MyDefinitionManager.Static.TryGetCubeBlockDefinition(mirrorDefId, out tempDef) && tempDef != null)
                {
                    mirrorDef = tempDef;
                    block.SubtypeName = mirrorDef.Id.SubtypeName;
                }
            }

            var orientation = block.BlockOrientation;
            var forwardVec = Base6Directions.GetVector(orientation.Forward);
            var upVec = Base6Directions.GetVector(orientation.Up);
            Matrix sourceMatrix = Matrix.CreateWorld(Vector3.Zero, forwardVec, upVec);

            Vector3 mirrorNormal = GetMirrorNormal(axis);

            MySymmetryAxisEnum blockMirrorAxis = MySymmetryAxisEnum.None;
            if (IsAligned(sourceMatrix.Right, mirrorNormal))
                blockMirrorAxis = MySymmetryAxisEnum.X;
            else if (IsAligned(sourceMatrix.Up, mirrorNormal))
                blockMirrorAxis = MySymmetryAxisEnum.Y;
            else if (IsAligned(sourceMatrix.Forward, mirrorNormal))
                blockMirrorAxis = MySymmetryAxisEnum.Z;

            MySymmetryAxisEnum symmetryOp = MySymmetryAxisEnum.None;
            switch (blockMirrorAxis)
            {
                case MySymmetryAxisEnum.X: symmetryOp = def.SymmetryX; break;
                case MySymmetryAxisEnum.Y: symmetryOp = def.SymmetryY; break;
                case MySymmetryAxisEnum.Z: symmetryOp = def.SymmetryZ; break;
            }

            Matrix targetMatrix = ApplySymmetryRotation(symmetryOp, sourceMatrix);

            var newForward = Base6Directions.GetClosestDirection(targetMatrix.Forward);
            var newUp = Base6Directions.GetClosestDirection(targetMatrix.Up);
            block.BlockOrientation = new SerializableBlockOrientation(newForward, newUp);
        }

        private static bool IsAligned(Vector3 a, Vector3 b)
        {
            return System.Math.Abs(System.Math.Abs(Vector3.Dot(a, b)) - 1.0f) < 0.001f;
        }

        private static Vector3 GetMirrorNormal(MirrorAxis axis)
        {
            switch (axis)
            {
                case MirrorAxis.X: return Vector3.Right;
                case MirrorAxis.Y: return Vector3.Up;
                default: return Vector3.Forward;
            }
        }

        private static Matrix ApplySymmetryRotation(MySymmetryAxisEnum symmetryOp, Matrix source)
        {
            float pi = MathHelper.Pi;
            float halfPi = MathHelper.PiOver2;

            switch (symmetryOp)
            {
                case MySymmetryAxisEnum.X:
                    return Matrix.CreateRotationX(pi) * source;
                case MySymmetryAxisEnum.Y:
                case MySymmetryAxisEnum.YThenOffsetX:
                    return Matrix.CreateRotationY(pi) * source;
                case MySymmetryAxisEnum.Z:
                case MySymmetryAxisEnum.ZThenOffsetX:
                    return Matrix.CreateRotationZ(pi) * source;

                case MySymmetryAxisEnum.HalfX:
                    return Matrix.CreateRotationX(-halfPi) * source;
                case MySymmetryAxisEnum.HalfY:
                    return Matrix.CreateRotationY(-halfPi) * source;
                case MySymmetryAxisEnum.HalfZ:
                    return Matrix.CreateRotationZ(-halfPi) * source;

                case MySymmetryAxisEnum.MinusHalfX:
                    return Matrix.CreateRotationX(halfPi) * source;
                case MySymmetryAxisEnum.MinusHalfY:
                    return Matrix.CreateRotationY(halfPi) * source;
                case MySymmetryAxisEnum.MinusHalfZ:
                    return Matrix.CreateRotationZ(halfPi) * source;

                case MySymmetryAxisEnum.XHalfY:
                    return Matrix.CreateRotationY(halfPi) * Matrix.CreateRotationX(pi) * source;
                case MySymmetryAxisEnum.YHalfY:
                    return Matrix.CreateRotationY(halfPi) * Matrix.CreateRotationY(pi) * source;
                case MySymmetryAxisEnum.ZHalfY:
                    return Matrix.CreateRotationY(halfPi) * Matrix.CreateRotationZ(pi) * source;

                case MySymmetryAxisEnum.XHalfX:
                    return Matrix.CreateRotationX(-halfPi) * Matrix.CreateRotationX(pi) * source;
                case MySymmetryAxisEnum.YHalfX:
                    return Matrix.CreateRotationX(-halfPi) * Matrix.CreateRotationY(pi) * source;
                case MySymmetryAxisEnum.ZHalfX:
                    return Matrix.CreateRotationX(-halfPi) * Matrix.CreateRotationZ(pi) * source;

                case MySymmetryAxisEnum.XHalfZ:
                    return Matrix.CreateRotationZ(-halfPi) * Matrix.CreateRotationX(pi) * source;
                case MySymmetryAxisEnum.YHalfZ:
                    return Matrix.CreateRotationZ(-halfPi) * Matrix.CreateRotationY(pi) * source;
                case MySymmetryAxisEnum.ZHalfZ:
                    return Matrix.CreateRotationZ(-halfPi) * Matrix.CreateRotationZ(pi) * source;

                case MySymmetryAxisEnum.XMinusHalfZ:
                    return Matrix.CreateRotationZ(halfPi) * Matrix.CreateRotationX(pi) * source;
                case MySymmetryAxisEnum.YMinusHalfZ:
                    return Matrix.CreateRotationZ(halfPi) * Matrix.CreateRotationY(pi) * source;
                case MySymmetryAxisEnum.ZMinusHalfZ:
                    return Matrix.CreateRotationZ(halfPi) * Matrix.CreateRotationZ(pi) * source;

                case MySymmetryAxisEnum.XMinusHalfX:
                    return Matrix.CreateRotationX(halfPi) * Matrix.CreateRotationX(pi) * source;
                case MySymmetryAxisEnum.YMinusHalfX:
                    return Matrix.CreateRotationX(halfPi) * Matrix.CreateRotationY(pi) * source;
                case MySymmetryAxisEnum.ZMinusHalfX:
                    return Matrix.CreateRotationX(halfPi) * Matrix.CreateRotationZ(pi) * source;

                case MySymmetryAxisEnum.None:
                default:
                    return source;
            }
        }

        private static Base6Directions.Direction SimpleMirrorDirection(Base6Directions.Direction dir, MirrorAxis axis)
        {
            switch (axis)
            {
                case MirrorAxis.X:
                    if (dir == Base6Directions.Direction.Left) return Base6Directions.Direction.Right;
                    if (dir == Base6Directions.Direction.Right) return Base6Directions.Direction.Left;
                    return dir;
                case MirrorAxis.Y:
                    if (dir == Base6Directions.Direction.Up) return Base6Directions.Direction.Down;
                    if (dir == Base6Directions.Direction.Down) return Base6Directions.Direction.Up;
                    return dir;
                case MirrorAxis.Z:
                    if (dir == Base6Directions.Direction.Forward) return Base6Directions.Direction.Backward;
                    if (dir == Base6Directions.Direction.Backward) return Base6Directions.Direction.Forward;
                    return dir;
                default:
                    return dir;
            }
        }
    }
}
