namespace ElGato_API.VMO.Community
{
    public class BlockListVMO
    {
        public List<BlockList> BlockList { get; set; } = new List<BlockList>();
    }

    public class BlockList
    {
        public string Name { get; set; }
        public string Pfp { get; set; }
        public string UserId { get; set; }
    }
}
