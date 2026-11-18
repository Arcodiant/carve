using System.Numerics;
using System.Text.Json;
using Carve.Scenario.Acceptance;
using Carve.Scenario.Crate;

namespace Carve.Server.Tests;

/// <summary>
/// RV-53: the acceptance step's silhouette reads <c>get_mesh</c> as it is written, with a deleted vertex or face null in its
/// slot and a holed face as {loop, holes}, and draws a holed face with its hole open.
/// </summary>
[Trait("Tier", "unit")]
public class SilhouetteTests
{
    private const int Size = 128;

    // get_mesh's shape: vertex 0 and face 0 deleted, face 1 a 2 m plate facing +Z with a 1 m hole, face 2 a plain triangle.
    private const string Plate = """
        {
          "vertices": [
            null,
            { "id": 1, "at": [-1, -1, 0] }, { "id": 2, "at": [1, -1, 0] }, { "id": 3, "at": [1, 1, 0] }, { "id": 4, "at": [-1, 1, 0] },
            { "id": 5, "at": [-0.5, -0.5, 0] }, { "id": 6, "at": [-0.5, 0.5, 0] }, { "id": 7, "at": [0.5, 0.5, 0] }, { "id": 8, "at": [0.5, -0.5, 0] },
            { "id": 9, "at": [1, -1, -0.5] }
          ],
          "faces": [
            null,
            { "id": 1, "loop": [1, 2, 3, 4], "holes": [[5, 6, 7, 8]], "normal": [0, 0, 1], "centre": [0, 0, 0] },
            { "id": 2, "vertices": [2, 9, 3], "normal": [1, 0, 0], "centre": [1, -0.33, -0.17] }
          ]
        }
        """;

    [Fact]
    public void A_deleted_slot_and_a_holed_face_are_read()
    {
        var mesh = Silhouette.Read(JsonDocument.Parse(Plate).RootElement);

        Assert.Null(mesh.Vertices[0]);
        Assert.Equal(new Vector3(0.5f, -0.5f, 0), mesh.Vertices[8]);
        Assert.Equal(2, mesh.Faces.Length);
        Assert.Equal([[1, 2, 3, 4], [5, 6, 7, 8]], mesh.Faces[0]);
        Assert.Equal([[2, 9, 3]], mesh.Faces[1]);
    }

    [Fact]
    public void A_holed_face_is_drawn_with_its_hole_open()
    {
        var mesh = Silhouette.Read(JsonDocument.Parse(Plate).RootElement);
        var inside = Silhouette.Predict([mesh], Vector3.UnitZ, Size);

        // The framing is the box of the live vertices only: the deleted slot is not at the origin of the box.
        var project = CameraPixels.Perspective(new Vector3(-1, -1, -0.5f), new Vector3(1, 1, 0), Vector3.UnitZ, Size);
        bool At(Vector3 p)
        {
            var s = project(p);
            return inside[(int)s.Y * Size + (int)s.X];
        }

        Assert.False(At(Vector3.Zero), "the hole's middle is drawn: the face was filled from its loop alone");
        Assert.True(At(new Vector3(0.75f, 0, 0)), "the plate's ring is not drawn");
        Assert.True(At(new Vector3(-0.75f, 0.75f, 0)), "the plate's corner is not drawn");
    }
}
