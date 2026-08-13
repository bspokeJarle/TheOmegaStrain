namespace Domain
{
    public class EngineTriangleMesh : ITriangleMesh
    {
        private IVector3? _normal1;
        private IVector3? _normal2;
        private IVector3? _normal3;
        private IVector3? _vert1;
        private IVector3? _vert2;
        private IVector3? _vert3;

        public IVector3? Normal1Raw => _normal1;
        public IVector3? Normal2Raw => _normal2;
        public IVector3? Normal3Raw => _normal3;
        public IVector3? Vert1Raw => _vert1;
        public IVector3? Vert2Raw => _vert2;
        public IVector3? Vert3Raw => _vert3;

        public IVector3 normal1
        {
            get => _normal1 ??= CreateVector();
            set => _normal1 = value;
        }

        public IVector3 normal2
        {
            get => _normal2 ??= CreateVector();
            set => _normal2 = value;
        }

        public IVector3 normal3
        {
            get => _normal3 ??= CreateVector();
            set => _normal3 = value;
        }

        public IVector3 vert1
        {
            get => _vert1 ??= CreateVector();
            set => _vert1 = value;
        }

        public IVector3 vert2
        {
            get => _vert2 ??= CreateVector();
            set => _vert2 = value;
        }

        public IVector3 vert3
        {
            get => _vert3 ??= CreateVector();
            set => _vert3 = value;
        }

        public long? landBasedPosition { get; set; }
        public float angle { get; set; }
        public bool? noHidden { get; set; }

        protected virtual IVector3 CreateVector() => new EngineVector3();
    }
}
