using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;

namespace CMakeCommentExtension
{
    /// <summary>
    /// Extension entrypoint for the VisualStudio.Extensibility extension.
    /// </summary>
    [VisualStudioContribution]
    internal class ExtensionEntrypoint : Extension
    {
        /// <inheritdoc/>
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            Metadata = new(
                    id: "CMakeCommentExtension.89fbacb7-313a-4e15-9f5b-2cb8dd767a1d",
                    version: this.ExtensionAssemblyVersion,
                    publisherName: "KSGCOM",
                    displayName: "CMake Comment Toggle",
                    description: "Toggle line comments in CMakeLists.txt and *.cmake files"),
        };

        /// <inheritdoc />
        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);

            // You can configure dependency injection here by adding services to the serviceCollection.
        }
    }
}
