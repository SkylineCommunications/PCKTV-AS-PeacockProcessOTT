/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2023	1.0.0.1		XXX, Skyline	Initial version
****************************************************************************
*/

namespace Rebuild_PCK_DOM_1
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private DomHelper innerDomHelper;
		private IEngine bigengine;
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			var pckinstanceId = engine.GetScriptParam("InstanceId").Value;
			bigengine = engine;
			try
			{
				var domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");
				innerDomHelper = domHelper;

				var mainFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(pckinstanceId)));
				var pckInstances = domHelper.DomInstances.Read(mainFilter);

				if (pckInstances.Count > 0)
				{
					var instance = pckInstances.First();

					var touchstreamId = String.Empty;
					var convivaId = String.Empty;
					var tagId = String.Empty;
					var eventId = String.Empty;

					foreach (var section in instance.Sections)
					{
						Func<SectionDefinitionID, SectionDefinition> sectionDefinitionFunc = SetSectionDefinitionById;
						section.Stitch(sectionDefinitionFunc);

						var sectionDefinition = section.GetSectionDefinition();
						if (!sectionDefinition.GetName().Contains("Instances"))
						{
							continue;
						}

						var fields = sectionDefinition.GetAllFieldDescriptors();

						foreach (var field in fields)
						{
							if (field.Name.Contains("Touchstream"))
							{
								touchstreamId = Convert.ToString(section.GetFieldValueById(field.ID).Value.Value);
							}
							else if (field.Name.Contains("Conviva"))
							{
								convivaId = Convert.ToString(section.GetFieldValueById(field.ID).Value.Value);
							}
							else if (field.Name.Contains("TAG"))
							{
								tagId = Convert.ToString(section.GetFieldValueById(field.ID).Value.Value);
							}
							else if (field.Name.Contains("Event ID"))
							{
								eventId = Convert.ToString(section.GetFieldValueById(field.ID).Value.Value);
							}
						}
					}

					FindAndDeleteInstance(domHelper, convivaId);
					FindAndDeleteTouchstreamInstances(domHelper, touchstreamId);
					FindAndDeleteTagInstances(domHelper, tagId);

					var eventManager = new EventManager(engine);
					if (eventManager.VLTable.RowExists(eventId))
					{
						eventManager.VLDomInstanceColumn.SetValue(eventId, String.Empty);
						eventManager.VLProcessStatusColumn.SetValue(eventId, (int)ProcessStatus.Idle);
						eventManager.ActivateButtonColumn.SetValue(eventId, 1);
					}
					else if (eventManager.SLETable.RowExists(eventId))
					{
						eventManager.SLEDomInstanceColumn.SetValue(eventId, String.Empty);
						eventManager.SLEProcessStatusColumn.SetValue(eventId, (int)ProcessStatus.Idle);
					}
					else
					{
						engine.GenerateInformation("Unable to find row corresponding to event ID: " + eventId);
					}

					domHelper.DomInstances.Delete(instance);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"Failed to rebuild provision on id {pckinstanceId}: {ex}");
			}
		}

		private void FindAndDeleteTagInstances(DomHelper domHelper, string tagId)
		{
			if (String.IsNullOrWhiteSpace(tagId) || !Guid.TryParse(tagId, out Guid id))
			{
				bigengine.GenerateInformation("Unable to handle instance id (TAG): " + tagId);
				return;
			}

			var tagFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(tagId)));
			var tagInstances = domHelper.DomInstances.Read(tagFilter);

			if (tagInstances.Count > 0)
			{
				var tagInstance = tagInstances.First();
				List<Guid> tagScanIds = GetChildIds(tagInstance, "Scan");
				foreach (var tagScanId in tagScanIds)
				{
					var tagScanFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(tagScanId));
					var tagScanInstances = domHelper.DomInstances.Read(tagScanFilter);
					if (tagScanInstances.Count > 0)
					{
						var tagScanInstance = tagScanInstances.First();
						var tagChannels = GetChildIds(tagScanInstance, "Channels");
						DeleteInstances(domHelper, tagChannels);
						domHelper.DomInstances.Delete(tagScanInstance);
					}
				}

				domHelper.DomInstances.Delete(tagInstance);
			}
		}

		private void DeleteInstances(DomHelper domHelper, List<Guid> instanceIds)
		{
			foreach (var instanceId in instanceIds)
			{
				var instanceFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(instanceId));
				var instances = domHelper.DomInstances.Read(instanceFilter);

				if (instances.Count > 0)
				{
					var instance = instances.First();
					domHelper.DomInstances.Delete(instance);
				}
			}
		}

		private List<Guid> GetChildIds(DomInstance instance, string childField)
		{
			var childIds = new List<Guid>();
			foreach (var section in instance.Sections)
			{
				Func<SectionDefinitionID, SectionDefinition> sectionDefinitionFunc = SetSectionDefinitionById;
				section.Stitch(sectionDefinitionFunc);

				var sectionDefinition = section.GetSectionDefinition();
				var fields = sectionDefinition.GetAllFieldDescriptors();

				foreach (var field in fields.Where(field => field.Name.Contains(childField)))
				{
					var fieldValue = section.GetFieldValueById(field.ID);
					if (fieldValue != null)
					{
						childIds.AddRange((List<Guid>)fieldValue.Value.Value);
					}
				}
			}

			return childIds;
		}

		private void FindAndDeleteInstance(DomHelper domHelper, string instanceId)
		{
			if (String.IsNullOrWhiteSpace(instanceId) || !Guid.TryParse(instanceId, out Guid id))
			{
				bigengine.GenerateInformation("Unable to handle instance id (Conviva): " + instanceId);
				return;
			}

			var filter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(instanceId)));
			var instances = domHelper.DomInstances.Read(filter);

			if (instances.Count > 0)
			{
				domHelper.DomInstances.Delete(instances.First());
			}
		}

		private void FindAndDeleteTouchstreamInstances(DomHelper domHelper, string instanceId)
		{
			if (String.IsNullOrWhiteSpace(instanceId) || !Guid.TryParse(instanceId, out Guid id))
			{
				bigengine.GenerateInformation("Unable to handle instance id (Touchstream): " + instanceId);
				return;
			}

			var filter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(instanceId)));
			var instances = domHelper.DomInstances.Read(filter);

			if (instances.Count > 0)
			{
				var instance = instances.First();
				var mediaTailorIds = GetChildIds(instance, "MediaTailor");

				DeleteInstances(domHelper, mediaTailorIds);

				domHelper.DomInstances.Delete(instance);
			}
		}

		private SectionDefinition SetSectionDefinitionById(SectionDefinitionID sectionDefinitionId)
		{
			return this.innerDomHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(sectionDefinitionId)).First();
		}
	}

	public enum ProcessStatus
	{
		NA = -1,
		Idle = 1,
		Processing = 2,
		Provisioned = 3,
		Deactivating = 4,
		Complete = 5,
	}

	public class EventManager
	{
		private const int SleDomInstance = 230;
		private const int SleProcessStatus = 231;
		private const int VlDomInstance = 2126;
		private const int VlProcessStatus = 2127;
		private const int ActivateButton = 2118;
		private const int Status = 2104;

		public IEngine Engine { get; private set; }

		public IDms Dms { get; private set; }

		public IDmsElement Element { get; set; }

		public IDmsTable VLTable { get; set; }

		public IDmsTable SLETable { get; set; }

		public IDmsColumn<int?> ActivateButtonColumn { get; set; }

		public IDmsColumn<string> StatusColumn { get; set; }

		public IDmsColumn<string> SLEDomInstanceColumn { get; set; }

		public IDmsColumn<string> VLDomInstanceColumn { get; set; }

		public IDmsColumn<int?> SLEProcessStatusColumn { get; set; }

		public IDmsColumn<int?> VLProcessStatusColumn { get; set; }

		public EventManager(IEngine engine)
		{
			this.Engine = engine;
			var dms = engine.GetDms();
			this.Dms = dms;
			Element = dms.GetElement("SLE Event Manager - LEM");
			VLTable = Element.GetTable(2100);
			SLETable = Element.GetTable(200);

			ActivateButtonColumn = VLTable.GetColumn<int?>(ActivateButton);
			StatusColumn = VLTable.GetColumn<string>(Status);

			SLEDomInstanceColumn = SLETable.GetColumn<string>(SleDomInstance);
			SLEProcessStatusColumn = SLETable.GetColumn<int?>(SleProcessStatus);
			VLDomInstanceColumn = VLTable.GetColumn<string>(VlDomInstance);
			VLProcessStatusColumn = VLTable.GetColumn<int?>(VlProcessStatus);
		}
	}
}