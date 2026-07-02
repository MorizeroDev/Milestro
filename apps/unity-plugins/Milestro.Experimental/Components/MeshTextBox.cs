using Milestro.Skia.TextLayout;
using UnityEngine;
using UnityEngine.UI;

namespace Milestro.Experimental.Components
{
    [AddComponentMenu("Milestro/Experimental/Mesh Text Box")]
    public class MeshTextBox : MaskableGraphic
    {
        private Paragraph? paragraphField = null;

        public Paragraph? Paragraph
        {
            set
            {
                paragraphField = value;
                SetVerticesDirty();
            }
            get => paragraphField;
        }

        [SerializeField] public Vector2 offsetPosition;

        [SerializeField] public float tolerance = 0.25f;

        [SerializeField] public Color textColor = Color.white;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (paragraphField == null)
            {
                return;
            }

            var path = paragraphField.ToPath(offsetPosition.x, offsetPosition.y);
            using var vertexData = path.ToAATriangles(tolerance);

            var vertexList = vertexData.GetVertices();
            Debug.Log(vertexList.Length);

            vh.Clear();
            var count = vertexList.Length;
            UIVertex vert = UIVertex.simpleVert;
            vert.color = textColor;
            for (int i = 0; i < count / 3 / 3; i++)
            {
                vert.position = new Vector3(vertexList[i * 9 + 0], vertexList[i * 9 + 1], vertexList[i * 9 + 2]);
                vh.AddVert(vert);
                vert.position = new Vector3(vertexList[i * 9 + 3], vertexList[i * 9 + 4], vertexList[i * 9 + 5]);
                vh.AddVert(vert);
                vert.position = new Vector3(vertexList[i * 9 + 6], vertexList[i * 9 + 7], vertexList[i * 9 + 8]);
                vh.AddVert(vert);
                vh.AddTriangle(i * 3, i * 3 + 1, i * 3 + 2);
            }
        }
    }
}
