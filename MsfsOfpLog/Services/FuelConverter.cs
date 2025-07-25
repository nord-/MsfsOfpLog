using static System.Globalization.CultureInfo;

namespace MsfsOfpLog.Services
{
    /// <summary>
    /// Utility class for converting fuel quantities between different units.
    /// SimConnect provides fuel data in gallons, but we need to work with kilograms.
    /// </summary>
    public static class FuelConverter
    {
        /// <summary>
        /// Aviation fuel density factor: kg per gallon
        /// This is the standard density for Jet A-1 fuel used in commercial aviation
        /// </summary>
        private const double KgPerGallon = 3.032;
        
        /// <summary>
        /// Convert fuel quantity from gallons to kilograms
        /// </summary>
        /// <param name="gallons">Fuel quantity in gallons</param>
        /// <returns>Fuel quantity in kilograms</returns>
        public static double GallonsToKg(double gallons) => gallons * KgPerGallon;        
        
        /// <summary>
        /// Convert fuel quantity from gallons to kilograms (rounded to integer)
        /// </summary>
        /// <param name="gallons">Fuel quantity in gallons</param>
        /// <returns>Fuel quantity in kilograms (rounded to nearest integer)</returns>
        public static int GallonsToKgInt(double gallons) => (int)Math.Round(gallons * KgPerGallon);

        /// <summary>
        /// Convert fuel quantity from gallons to tonnes (formatted string)
        /// </summary>
        /// <param name="gallons">Fuel quantity in gallons</param>
        /// <returns>Formatted string representing fuel quantity in tonnes</returns>
        public static string GallonsToTonnesString(this double gallons) => GallonsToKg(gallons).KgToTonnesString();

        /// <summary>
        /// Convert fuel quantity from kilograms to gallons
        /// </summary>
        /// <param name="kg">Fuel quantity in kilograms</param>
        /// <returns>Fuel quantity in gallons</returns>
        public static double KgToGallons(double kg) => kg / KgPerGallon;        
        
        /// <summary>
        /// Convert fuel quantity from kilograms to tonnes
        /// </summary>
        /// <param name="kg">Fuel quantity in kilograms</param>
        /// <returns>Fuel quantity in tonnes</returns>
        public static double KgToTonnes(double kg) => kg / 1000.0;

        /// <summary>
        /// Convert fuel quantity from kilograms to tonnes (formatted string)
        /// </summary>
        /// <param name="kg">Fuel quantity in kilograms</param>
        /// <returns>Formatted string representing fuel quantity in tonnes</returns>
        public static string KgToTonnesString(this double kg) => (kg / 1000.0).ToString("F1", InvariantCulture);

        /// <summary>
        /// Convert fuel quantity from gallons to tonnes
        /// </summary>
        /// <param name="gallons">Fuel quantity in gallons</param>
        /// <returns>Fuel quantity in tonnes</returns>
        public static double GallonsToTonnes(double gallons) => GallonsToKg(gallons) / 1000.0;
        
        /// <summary>
        /// Get the conversion factor (for reference/documentation)
        /// </summary>
        public static double GetKgPerGallonFactor() => KgPerGallon;        
    }
}
