
namespace SkillLibrary
{
	
	public class CurrentLED
	{
		public ushort Red { get; set; }
		public ushort Green { get; set; }
		public ushort Blue { get; set; }

		public CurrentLED(ushort red, ushort green, ushort blue)
		{
			Red = red;
			Green = green;
			Blue = blue;
		}
	}
}