namespace ElGato_API.VMO.Community
{
    public class AcessibleVMO
    {
        public bool Acessible { get; set; } = true;
        public UnacessilibityReason? UnacessilibityReason { get; set; }
    }

    public enum UnacessilibityReason
    {
        Private, 
        Blocked,
        Other
    }
}
