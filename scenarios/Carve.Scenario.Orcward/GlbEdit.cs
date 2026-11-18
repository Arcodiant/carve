using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace Carve.Scenario.Orcward;

/// <summary>
/// A .glb opened for tampering, so the scenario can show each check failing on a file that is wrong in one way: the skin's joints
/// swapped, two inverse binds swapped, every triangle's winding reversed. Only the JSON chunk and the binary chunk are touched.
/// </summary>
internal sealed class GlbEdit
{
    private readonly JsonNode _json;
    private readonly byte[] _bin;

    private GlbEdit(JsonNode json, byte[] bin)
    {
        _json = json;
        _bin = bin;
    }

    public static GlbEdit Read(byte[] glb)
    {
        var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(12));
        var json = JsonNode.Parse(Encoding.UTF8.GetString(glb, 20, jsonLength))!;
        var binAt = 20 + jsonLength;
        var binLength = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(binAt));
        return new GlbEdit(json, glb.AsSpan(binAt + 8, binLength).ToArray());
    }

    public byte[] Write()
    {
        var json = Encoding.UTF8.GetBytes(_json.ToJsonString());
        var jsonPadded = (json.Length + 3) & ~3;
        var binPadded = (_bin.Length + 3) & ~3;
        var total = 12 + 8 + jsonPadded + 8 + binPadded;
        var glb = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(glb, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(8), total);
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(12), jsonPadded);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), 0x4E4F534A);
        json.CopyTo(glb, 20);
        Array.Fill(glb, (byte)' ', 20 + json.Length, jsonPadded - json.Length);
        var binAt = 20 + jsonPadded;
        BinaryPrimitives.WriteInt32LittleEndian(glb.AsSpan(binAt), binPadded);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binAt + 4), 0x004E4942);
        _bin.CopyTo(glb, binAt + 8);
        return glb;
    }

    private JsonArray Joints => _json["skins"]![0]!["joints"]!.AsArray();

    private int JointSlot(string bone)
    {
        var nodes = _json["nodes"]!.AsArray();
        for (var i = 0; i < Joints.Count; i++)
        {
            if ((string?)nodes[(int)Joints[i]!]!["name"] == bone)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"no joint named {bone}");
    }

    /// <summary>Swaps two entries of the skin's <c>joints</c>, leaving the vertices' joint indices and the inverse binds as they were.</summary>
    public void SwapJoints(string a, string b)
    {
        int ia = JointSlot(a), ib = JointSlot(b);
        var joints = Joints;
        var (ja, jb) = ((int)joints[ia]!, (int)joints[ib]!);
        joints[ia] = jb;
        joints[ib] = ja;
    }

    private (int Offset, int Count, int Component) Accessor(int index)
    {
        var accessor = _json["accessors"]![index]!;
        var view = _json["bufferViews"]![(int)accessor["bufferView"]!]!;
        var offset = ((int?)view["byteOffset"] ?? 0) + ((int?)accessor["byteOffset"] ?? 0);
        return (offset, (int)accessor["count"]!, (int)accessor["componentType"]!);
    }

    /// <summary>Swaps two bones' inverse bind matrices, leaving the joints as they were.</summary>
    public void SwapInverseBinds(string a, string b)
    {
        var (offset, count, _) = Accessor((int)_json["skins"]![0]!["inverseBindMatrices"]!);
        int ia = JointSlot(a), ib = JointSlot(b);
        if (ia >= count || ib >= count)
        {
            throw new InvalidOperationException("inverse bind accessor is shorter than the joints");
        }

        var tmp = _bin.AsSpan(offset + ia * 64, 64).ToArray();
        _bin.AsSpan(offset + ib * 64, 64).CopyTo(_bin.AsSpan(offset + ia * 64, 64));
        tmp.CopyTo(_bin, offset + ib * 64);
    }

    /// <summary>
    /// Swaps the base colours of the two named materials, as an export with a material index off between primitives would draw
    /// them: each group in the other's colour, the geometry untouched.
    /// </summary>
    public void SwapBaseColours(string a, string b)
    {
        JsonNode Pbr(string name) =>
            _json["materials"]!.AsArray().Single(m => (string?)m!["name"] == name)!["pbrMetallicRoughness"]!;
        var (pa, pb) = (Pbr(a), Pbr(b));
        var colourA = pa["baseColorFactor"]!.DeepClone();
        pa["baseColorFactor"] = pb["baseColorFactor"]!.DeepClone();
        pb["baseColorFactor"] = colourA;
    }

    /// <summary>Reverses every triangle's winding (indices 1 and 2 swapped), once per index accessor.</summary>
    public int ReverseWinding()
    {
        var done = new HashSet<int>();
        var triangles = 0;
        foreach (var mesh in _json["meshes"]!.AsArray())
        {
            foreach (var primitive in mesh!["primitives"]!.AsArray())
            {
                var index = (int)primitive!["indices"]!;
                if (!done.Add(index))
                {
                    continue;
                }

                var (offset, count, component) = Accessor(index);
                var size = component == 5125 ? 4 : component == 5123 ? 2 : throw new InvalidOperationException($"index component {component}");
                for (var t = 0; t + 2 < count; t += 3)
                {
                    var one = _bin.AsSpan(offset + (t + 1) * size, size);
                    var two = _bin.AsSpan(offset + (t + 2) * size, size);
                    var tmp = one.ToArray();
                    two.CopyTo(one);
                    tmp.CopyTo(two);
                    triangles++;
                }
            }
        }

        return triangles;
    }
}
