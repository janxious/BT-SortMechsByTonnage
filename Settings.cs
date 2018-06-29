namespace SortByTonnage
{
    public class Settings
    {
        public const string ModName = "SortByTonnage";
        public const string ModId   = "com.joelmeador.SortByTonnage";

        public bool debug = false;

        public bool orderByCbillValue = false;
        public bool OrderByCbillValue => orderByCbillValue;
        
        public bool orderByNickname = false;
        public bool OrderByNickname => orderByNickname;
    }
}