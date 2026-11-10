using Turniejo.SharedKernel.Results;

namespace Turniejo.Teams.Application.TeamLabels
{
    internal static class TeamLabelErrors
    {
        public static readonly Error DuplicateName =
            Error.Conflict("Name", "A label with this name already exists");

        public static readonly Error TeamOutsideOrganization =
            Error.Validation("TeamIds", "Team does not belong to this organization");

        public static readonly Error LabelOutsideOrganization =
            Error.Validation("LabelIds", "Label does not belong to this organization");
    }
}
