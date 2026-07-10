using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace RecruitPro.API.OData;

// EDM model for the /odata route prefix. Two read-only entity sets: Jobs and Applications.
public static class ODataEdmModel
{
    public static IEdmModel Build()
    {
        var builder = new ODataConventionModelBuilder();
        builder.EntitySet<JobODataDto>("Jobs");
        builder.EntitySet<ApplicationODataDto>("Applications");
        return builder.GetEdmModel();
    }
}
