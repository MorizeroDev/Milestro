using Milestro.Model;

namespace Milestro.Components
{
    /// <summary>Validates a complete filtered edit before it changes the value.</summary>
    public delegate TextInputEditResult TextInputValidateEdit(TextInputEditContext context);
}
