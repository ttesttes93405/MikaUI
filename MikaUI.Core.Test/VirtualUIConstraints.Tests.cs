using System;
using MikaUI;
using MikaUI.Plugin;
using NUnit.Framework;

namespace Tests.Core
{
    public class VirtualUIConstraintsTests
    {
        struct ValueVirtualUI : IVirtualUI { }

        [Test]
        public void VirtualCreationApisRejectValueTypes()
        {
            var methods = new[]
            {
                typeof(UIManager<DummyUI, DummyContainer, object>).GetMethod("CreateVirtual"),
                typeof(IUIElementProvider<DummyUI, DummyContainer>).GetMethod("GetVirtualUIElement"),
                typeof(IPluginVirtualUICreatedHandler).GetMethod("OnVirtualUICreated"),
                typeof(InjectUIManagerPlugin<DummyUI, DummyContainer, object>).GetMethod("OnVirtualUICreated"),
            };

            foreach (var method in methods)
            {
                Assert.Throws<ArgumentException>(() => method.MakeGenericMethod(typeof(ValueVirtualUI)), method.ToString());
                Assert.DoesNotThrow(() => method.MakeGenericMethod(typeof(DummyVirtualUI)), method.ToString());
            }
        }

    }
}
