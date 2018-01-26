using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyIoC
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public class ImportAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Constructor)]
	public class ImportConstructorAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Class)]
	public class ExportAttribute : Attribute
	{
		public ExportAttribute()
		{ }

		public ExportAttribute(Type contract)
		{
			Contract = contract;
		}

		public Type Contract { get; private set; }
	}


	[Export]
	public class ContractBLL {}

	[Export]
	public class ContractDLL { }

}
