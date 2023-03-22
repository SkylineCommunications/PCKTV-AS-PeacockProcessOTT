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

using System;
using System.Collections.Generic;
using System.Linq;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;
using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
using Skyline.DataMiner.Net.Messages.SLDataGateway;
using Skyline.DataMiner.Net.Sections;

/// <summary>
/// DataMiner Script Class.
/// </summary>
public class Script
{
	private static readonly string BehaviorDefinitionName = "Peacock Provision Behavior";
	private static readonly string DefinitionName = "Peacock Provision";
	private Engine engine;
	private DomHelper domHelper;

	/// <summary>
	/// The Script entry point.
	/// </summary>
	/// <param name="engine">Link with SLAutomation process.</param>
	public void Run(Engine engine)
	{
		this.engine = engine;
		domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");

		var peacockProvisionDomDefinition = CreateDomDefinition();
		if (peacockProvisionDomDefinition != null)
		{
			var domDefinition = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal(peacockProvisionDomDefinition.Name));
			if (domDefinition.Any())
			{
				peacockProvisionDomDefinition.ID = domDefinition.FirstOrDefault()?.ID;
				domHelper.DomDefinitions.Update(peacockProvisionDomDefinition);
			}
			else
			{
				domHelper.DomDefinitions.Create(peacockProvisionDomDefinition);
			}
		}
	}

	private DomDefinition CreateDomDefinition()
	{
		try
		{
			// Create SectionDefinitions
			var provisionInfoSectionDefinitions = SectionDefinitions.CreateProvisionInfoServiceDefinition(domHelper);
			var domInstancesSectionDefinitions = SectionDefinitions.CreateDomInstancesServiceDefinition(engine, domHelper);

			var sections = new List<SectionDefinition> { provisionInfoSectionDefinitions, domInstancesSectionDefinitions };

			// Create DomBehaviorDefinition
			var behavior = domHelper.DomBehaviorDefinitions.Read(DomBehaviorDefinitionExposers.Name.Equal(BehaviorDefinitionName));
			if (!behavior.Any())
			{
				var domBehaviorDefinition = BehaviorDefinitions.CreateDomBehaviorDefinition(sections);
				domBehaviorDefinition = domHelper.DomBehaviorDefinitions.Create(domBehaviorDefinition);
				behavior = new List<DomBehaviorDefinition> { domBehaviorDefinition };
			}

			// Create DOMDefinition
			return new DomDefinition
			{
				Name = DefinitionName,
				SectionDefinitionLinks = new List<SectionDefinitionLink> { new SectionDefinitionLink(provisionInfoSectionDefinitions.GetID()), new SectionDefinitionLink(domInstancesSectionDefinitions.GetID()) },
				DomBehaviorDefinitionId = behavior.FirstOrDefault()?.ID,
			};
		}
		catch (Exception ex)
		{
			engine.Log($"error on CreateDomDefinition method with exception {ex}");
			return null;
		}
	}

	public class SectionDefinitions
	{
		public static SectionDefinition CreateDomInstancesServiceDefinition(Engine engine, DomHelper domHelper)
		{
			var convivaDefinitionId = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal("Conviva")).First().ID.Id;
			engine.Log($"convivaDefinitionId: {convivaDefinitionId}");

			var convivaFieldDescriptor = CreateConvivaDomInstanceFieldDescriptorObject<Guid>("Conviva", "Link to the DOM Instance that contains the information for Conviva provisioning.", convivaDefinitionId);
			var tagFieldDescriptor = CreateDomInstanceFieldDescriptorObject<Guid>("TAG", "Link to the DOM Instance that contains the information for TAG provisioning.");
			var touchstreamFieldDescriptor = CreateDomInstanceFieldDescriptorObject<Guid>("Touchstream", "Link to the DOM Instance that contains the information for TS provisioning.");

			List<FieldDescriptor> fieldDescriptors = new List<FieldDescriptor>
			{
				convivaFieldDescriptor,
				tagFieldDescriptor,
				touchstreamFieldDescriptor,
			};

			var domInstanceSection = CreateOrUpdateSection("DOM Instances", domHelper, fieldDescriptors);

			return domInstanceSection;
		}

		public static SectionDefinition CreateProvisionInfoServiceDefinition(DomHelper domHelper)
		{
			var provisionNameFieldDescriptor = CreateFieldDescriptorObject<string>("Provision Name", "A name to describe the Event or Channel being provisioned.");
			var eventIdFieldDescriptor = CreateFieldDescriptorObject<string>("Event ID", "Unique ID to link the provision to an Event or Channel.");
			var sourceElementFieldDescriptor = CreateFieldDescriptorObject<string>("Source Element", "A DMAID/ELEMID/PID that has been configured to receive process updates (if configured).");

			List<FieldDescriptor> fieldDescriptors = new List<FieldDescriptor>
			{
				provisionNameFieldDescriptor,
				eventIdFieldDescriptor,
				sourceElementFieldDescriptor,
			};

			var provisionInfoSection = CreateOrUpdateSection("Provision Info", domHelper, fieldDescriptors);

			return provisionInfoSection;
		}

		private static SectionDefinition CreateOrUpdateSection(string name, DomHelper domHelper, List<FieldDescriptor> fieldDescriptors)
		{
			var domInstancesSectionDefinition = new CustomSectionDefinition
			{
				Name = name,
			};

			var domInstanceSection = domHelper.SectionDefinitions.Read(SectionDefinitionExposers.Name.Equal(domInstancesSectionDefinition.Name));
			SectionDefinition sectionDefinition;
			if (!domInstanceSection.Any())
			{
				foreach (var field in fieldDescriptors)
				{
					domInstancesSectionDefinition.AddOrReplaceFieldDescriptor(field);
				}

				sectionDefinition = domHelper.SectionDefinitions.Create(domInstancesSectionDefinition) as CustomSectionDefinition;
			}
			else
			{
				// Update Section Definition (Add missing fieldDescriptors)
				sectionDefinition = UpdateSectionDefinition(domHelper, fieldDescriptors, domInstanceSection);
			}

			return sectionDefinition;
		}

		private static SectionDefinition UpdateSectionDefinition(DomHelper domHelper, List<FieldDescriptor> fieldDescriptorList, List<SectionDefinition> sectionDefinition)
		{
			var existingSectionDefinition = sectionDefinition.First() as CustomSectionDefinition;
			var previousFieldNames = existingSectionDefinition.GetAllFieldDescriptors().Select(x => x.Name).ToList();
			List<FieldDescriptor> fieldDescriptorsToAdd = new List<FieldDescriptor>();

			// Check if there's a fieldDefinition to add
			foreach (var newfieldDescriptor in fieldDescriptorList)
			{
				if (!previousFieldNames.Contains(newfieldDescriptor.Name))
				{
					fieldDescriptorsToAdd.Add(newfieldDescriptor);
				}
			}

			if (fieldDescriptorsToAdd.Count > 0)
			{
				foreach (var field in fieldDescriptorsToAdd)
				{
					existingSectionDefinition.AddOrReplaceFieldDescriptor(field);
				}

				existingSectionDefinition = domHelper.SectionDefinitions.Update(existingSectionDefinition) as CustomSectionDefinition;
			}

			return existingSectionDefinition;
		}

		private static FieldDescriptor CreateFieldDescriptorObject<T>(string fieldName, string toolTip)
		{
			return new FieldDescriptor
			{
				FieldType = typeof(T),
				Name = fieldName,
				Tooltip = toolTip,
			};
		}

		private static DomInstanceFieldDescriptor CreateDomInstanceFieldDescriptorObject<T>(string fieldName, string toolTip)
		{
			return new DomInstanceFieldDescriptor
			{
				FieldType = typeof(T),
				Name = fieldName,
				Tooltip = toolTip,
			};
		}

		private static DomInstanceFieldDescriptor CreateConvivaDomInstanceFieldDescriptorObject<T>(string fieldName, string toolTip, Guid definitionId)
		{
			var field = new DomInstanceFieldDescriptor("process_automation")
			{
				FieldType = typeof(T),
				Name = fieldName,
				Tooltip = toolTip,
			};

			field.DomDefinitionIds.Add(new DomDefinitionId(definitionId));
			return field;
		}
	}

	public class BehaviorDefinitions
	{
		public static DomBehaviorDefinition CreateDomBehaviorDefinition(List<SectionDefinition> sections)
		{
			var statuses = new List<DomStatus>
			{
				new DomStatus("draft", "Draft"),
				new DomStatus("ready", "Ready"),
				new DomStatus("in_progress", "In Progress"),
				new DomStatus("active", "Active"),
				new DomStatus("deactivate", "Deactivate"),
				new DomStatus("reprovision", "Reprovision"),
				new DomStatus("complete", "Complete"),
			};

			var transitions = new List<DomStatusTransition>
			{
				new DomStatusTransition("draft_to_ready", "draft", "ready"),
				new DomStatusTransition("ready_to_inprogress", "ready", "in_progress"),
				new DomStatusTransition("inprogress_to_active", "in_progress", "active"),
				new DomStatusTransition("active_to_deactivate", "active", "deactivate"),
				new DomStatusTransition("active_to_reprovision", "active", "reprovision"),
				new DomStatusTransition("deactivate_to_complete", "deactivate", "complete"),
				new DomStatusTransition("reprovision_to_inprogress", "reprovision", "in_progress"),
				new DomStatusTransition("complete_to_ready", "complete", "ready"),
			};

			return new DomBehaviorDefinition
			{
				Name = BehaviorDefinitionName,
				InitialStatusId = "draft",
				Statuses = statuses,
				StatusTransitions = transitions,
				StatusSectionDefinitionLinks = GetStatusLinks(sections),
			};
		}

		private static List<DomStatusSectionDefinitionLink> GetStatusLinks(List<SectionDefinition> sections)
		{
			Dictionary<string, List<FieldDescriptorID>> fieldsList = GetFieldDescriptorDictionary(sections);

			var draftStatusLinks = StatusSectionDefinitions.GetDraftSectionDefinitionLinks(sections, fieldsList, "draft", false);
			var readyStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "ready", true);
			var inprogressStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "in_progress", true);
			var activeStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "active", true);
			var deactivateStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "deactivate", true);
			var reprovisionStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "reprovision", true);
			var completeStatusLinks = StatusSectionDefinitions.GetSectionDefinitionLinks(sections, fieldsList, "complete", true);

			return draftStatusLinks.Concat(readyStatusLinks).Concat(inprogressStatusLinks).Concat(activeStatusLinks).Concat(deactivateStatusLinks).Concat(reprovisionStatusLinks).Concat(completeStatusLinks).ToList();
		}

		private static Dictionary<string, List<FieldDescriptorID>> GetFieldDescriptorDictionary(List<SectionDefinition> sections)
		{
			Dictionary<string, List<FieldDescriptorID>> fieldsList = new Dictionary<string, List<FieldDescriptorID>>();
			foreach (var section in sections)
			{
				var fields = section.GetAllFieldDescriptors();
				foreach (var field in fields)
				{
					var sectionName = section.GetName();
					if (!fieldsList.ContainsKey(sectionName))
					{
						fieldsList[sectionName] = new List<FieldDescriptorID>();
					}

					fieldsList[sectionName].Add(field.ID);
				}
			}

			return fieldsList;
		}

		public class StatusSectionDefinitions
		{
			public static List<DomStatusSectionDefinitionLink> GetDraftSectionDefinitionLinks(List<SectionDefinition> sections, Dictionary<string, List<FieldDescriptorID>> fieldsList, string status, bool readOnly)
			{
				var sectionLinks = new List<DomStatusSectionDefinitionLink>();

				var fieldDescriptors = sections.First(x => x.GetName().Equals("Provision Info")).GetAllFieldDescriptors().ToList();
				var requiredFieldDescriptorIDs = fieldDescriptors.FindAll(x => x.Name.Equals("Provision Name") || x.Name.Equals("Event ID")).Select(x => x.ID).ToList();

				foreach (var section in sections)
				{
					var statusLinkId = new DomStatusSectionDefinitionLinkId(status, section.GetID());

					var statusLink = new DomStatusSectionDefinitionLink(statusLinkId);

					foreach (var fieldId in fieldsList[section.GetName()])
					{
						statusLink.FieldDescriptorLinks.Add(new DomStatusFieldDescriptorLink(fieldId)
						{
							Visible = true,
							ReadOnly = readOnly,
							RequiredForStatus = requiredFieldDescriptorIDs.Contains(fieldId),
						});
					}

					sectionLinks.Add(statusLink);
				}

				return sectionLinks;
			}

			public static List<DomStatusSectionDefinitionLink> GetDraftStatusSectionDefinitionLinks(SectionDefinition provisionInfoSectionDefinition, SectionDefinition domInstancesSectionDefinition, Dictionary<string, FieldDescriptorID> fieldsList)
			{
				var draftProvisionInfoStatusLink = new DomStatusSectionDefinitionLinkId("draft", provisionInfoSectionDefinition.GetID());
				var draftDomInstanceStatusLink = new DomStatusSectionDefinitionLinkId("draft", domInstancesSectionDefinition.GetID());

				var draftStatusLinkProvisionInfo = new DomStatusSectionDefinitionLink(draftProvisionInfoStatusLink)
				{
					FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
				{
					new DomStatusFieldDescriptorLink(fieldsList["Provision Name"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = true,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Event ID"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = true,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Source Element"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = false,
					},
				},
				};
				var draftStatusLinkDomInstance = new DomStatusSectionDefinitionLink(draftDomInstanceStatusLink)
				{
					FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
				{
					new DomStatusFieldDescriptorLink(fieldsList["Conviva"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = false,
					},
					new DomStatusFieldDescriptorLink(fieldsList["TAG"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = false,
					},
					new DomStatusFieldDescriptorLink(fieldsList["Touchstream"])
					{
						Visible = true,
						ReadOnly = false,
						RequiredForStatus = false,
					},
				},
				};

				return new List<DomStatusSectionDefinitionLink>() { draftStatusLinkProvisionInfo, draftStatusLinkDomInstance };
			}

			public static List<DomStatusSectionDefinitionLink> GetSectionDefinitionLinks(List<SectionDefinition> sections, Dictionary<string, List<FieldDescriptorID>> fieldsList, string status, bool readOnly)
			{
				var sectionLinks = new List<DomStatusSectionDefinitionLink>();
				foreach (var section in sections)
				{
					var statusLinkId = new DomStatusSectionDefinitionLinkId(status, section.GetID());

					var statusLink = new DomStatusSectionDefinitionLink(statusLinkId);

					foreach (var fieldId in fieldsList[section.GetName()])
					{
						statusLink.FieldDescriptorLinks.Add(new DomStatusFieldDescriptorLink(fieldId)
						{
							Visible = true,
							ReadOnly = readOnly,
							RequiredForStatus = false,
						});
					}

					sectionLinks.Add(statusLink);
				}

				return sectionLinks;
			}
		}
	}
}