using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IntervalsMcp.Tools;

[McpServerToolType]
public static class WorkoutTools
{
    [McpServerTool, Description(
        "Lista los workouts guardados en la biblioteca personal de Intervals.icu: plantillas reutilizables, " +
        "distintas de los eventos agendados en el calendario (ver list_events).")]
    public static Task<string> ListWorkouts(IntervalsIcuClient client, CancellationToken ct = default) =>
        client.ListWorkoutsAsync(ct);

    [McpServerTool, Description("Obtiene el detalle de un workout guardado en la biblioteca de Intervals.icu, por su id.")]
    public static Task<string> GetWorkout(
        IntervalsIcuClient client,
        [Description("El id numérico del workout.")] long workoutId,
        CancellationToken ct = default)
        => client.GetWorkoutAsync(workoutId, ct);

    [McpServerTool, Description(
        "Lista las carpetas de la biblioteca de workouts de Intervals.icu. Útil para elegir un folderId al llamar create_workout, " +
        "o para ver qué carpetas ya existen antes de crear una nueva categoría (ej. \"Series de pista\", \"Cuestas\").")]
    public static Task<string> ListFolders(IntervalsIcuClient client, CancellationToken ct = default) =>
        client.ListFoldersAsync(ct);

    [McpServerTool, Description(
        "Guarda un workout como plantilla reutilizable en la biblioteca de Intervals.icu (no lo agenda en el calendario, " +
        "para eso ver create_event). Usar la misma sintaxis de texto plano que create_event para describir la estructura, ej.: " +
        "\"- 15m 55% Warmup\\n3x\\n- 1m 150%\\n- 1m 50%\\n- 15m 55% Cooldown\". Para agendar esta plantilla más adelante, " +
        "copiar su description al llamar create_event.")]
    public static Task<string> CreateWorkout(
        IntervalsIcuClient client,
        [Description("Nombre de la plantilla, ej. \"Intervalos de pista 400m\".")] string name,
        [Description("Tipo de deporte, ej. Run, Ride, Swim.")] string type,
        [Description("Estructura del entreno con la sintaxis de Intervals.icu. Omitir para una plantilla sin estructura definida.")] string? description = null,
        [Description("Id de la carpeta donde guardarla (ver list_folders). Si se omite, se reusa la primera carpeta existente o se crea una llamada \"Workouts\".")] long? folderId = null,
        CancellationToken ct = default)
        => client.CreateWorkoutAsync(name, type, description, folderId, ct);

    [McpServerTool, Description("Actualiza un workout guardado en la biblioteca de Intervals.icu. Solo se modifican los campos que se pasan.")]
    public static Task<string> UpdateWorkout(
        IntervalsIcuClient client,
        [Description("El id numérico del workout a actualizar.")] long workoutId,
        [Description("Nuevo nombre. Omitir para no cambiarlo.")] string? name = null,
        [Description("Nuevo tipo de deporte. Omitir para no cambiarlo.")] string? type = null,
        [Description("Nueva estructura/descripción. Omitir para no cambiarla.")] string? description = null,
        CancellationToken ct = default)
        => client.UpdateWorkoutAsync(workoutId, name, type, description, ct);

    [McpServerTool, Description("Elimina un workout de la biblioteca de Intervals.icu. Esta acción no se puede deshacer.")]
    public static Task<string> DeleteWorkout(
        IntervalsIcuClient client,
        [Description("El id numérico del workout a eliminar.")] long workoutId,
        CancellationToken ct = default)
        => client.DeleteWorkoutAsync(workoutId, ct);
}
