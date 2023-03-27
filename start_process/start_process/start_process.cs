namespace Script
{
    using System;
    using System.Linq;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.MessageHandler;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Actions;
    using Skyline.DataMiner.Net.Messages.SLDataGateway;
    using Skyline.DataMiner.Net.Sections;

    public class Script
    {
        private DomHelper innerDomHelper;

        /// <summary>
        /// The Script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
        public void Run(Engine engine)
        {
            engine.ExitFail("This script should be executed using the 'OnDomAction' entry point");
        }

        [AutomationEntryPoint(AutomationEntryPointType.Types.OnDomAction)]
        public void OnDomActionMethod(IEngine engine, ExecuteScriptDomActionContext context)
        {
            var process = engine.GetScriptParam("process").Value;
            var transition = engine.GetScriptParam("transition").Value;
            var keyField = engine.GetScriptParam("key").Value;

            var instanceId = context.ContextId as DomInstanceId;
            innerDomHelper = new DomHelper(engine.SendSLNetMessages, instanceId.ModuleId);
            string businessKey = GetBusinessKey(keyField, instanceId);

            innerDomHelper.DomInstances.DoStatusTransition(instanceId, transition);
            ProcessHelper.PushToken(process, businessKey, instanceId);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1101:Prefix local calls with this", Justification = "Ignored")]
        private string GetBusinessKey(string keyField, DomInstanceId instanceId)
        {
            string businessKey = "default";
            var dominstance = DomInstanceExposers.Id.Equal(instanceId);
            var instance = innerDomHelper.DomInstances.Read(dominstance).First();
            var instanceSet = false;
            var keyFound = false;
            foreach (var section in instance.Sections)
            {
                Func<SectionDefinitionID, SectionDefinition> sectionDefinitionFunc = SetSectionDefinitionById;
                section.Stitch(sectionDefinitionFunc);

                foreach (var field in section.FieldValues)
                {
                    if (field.GetFieldDescriptor().Name.Contains("InstanceId"))
                    {
                        instance.AddOrUpdateFieldValue(section.GetSectionDefinition(), field.GetFieldDescriptor(), instanceId.Id.ToString());
                        instanceSet = true;
                    }

                    if (field.GetFieldDescriptor().Name == keyField)
                    {
                        businessKey = field.Value.ToString();
                        keyFound = true;
                    }

                    if (keyFound && instanceSet)
                    {
                        innerDomHelper.DomInstances.Update(instance);
                        return businessKey;
                    }
                }
            }

            return businessKey;
        }

        private SectionDefinition SetSectionDefinitionById(SectionDefinitionID sectionDefinitionId)
        {
            return innerDomHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(sectionDefinitionId)).First();
        }
    }
}