using ECK1.AsyncApi.Attributes;
using ECK1.CommandsAPI.Domain.Tasks;
using ECK1.Contracts.Shared;
using ECK1.Orleans;
using MediatR;
using Orleans;
using System.Text.Json.Serialization;
using TrackerTaskStatus = ECK1.CommandsAPI.Domain.Tasks.TaskStatus;

namespace ECK1.CommandsAPI.Commands;

[Newtonsoft.Json.JsonConverter(typeof(Polymorph<ITaskCommand>), "$type")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CreateTaskCommand), nameof(CreateTaskCommand))]
[JsonDerivedType(typeof(UpdateTaskCommand), nameof(UpdateTaskCommand))]
[JsonDerivedType(typeof(DeleteTaskCommand), nameof(DeleteTaskCommand))]
[Command]
[Topic(TopicConfigKey = "Kafka:TaskCommandsTopic")]
public interface ITaskCommand : IGrainKeyResolver<TaskItem>, IRequest<(ICommandResult, TaskItem)>;

[GenerateSerializer]
[Route("POST", "/api/async/task")]
public record CreateTaskCommand(
    string Title,
    string Description,
    TrackerTaskStatus Status,
    TaskPriority Priority,
    string Assignee,
    DateTime? DueDateUtc,
    List<string> Tags) : ITaskCommand;

[GenerateSerializer]
[Route("PUT", "/api/async/task/{id}")]
public record UpdateTaskCommand(
    [property: FromRoute("id")] Guid Id,
    string Title,
    string Description,
    TrackerTaskStatus Status,
    TaskPriority Priority,
    string Assignee,
    DateTime? DueDateUtc,
    List<string> Tags,
    int ExpectedVersion) : ITaskCommand, IValueId<Guid>;

[GenerateSerializer]
[Route("DELETE", "/api/async/task/{id}")]
[RequirePermissionAsync("delete")]
public record DeleteTaskCommand(
    [property: FromRoute("id")] Guid Id,
    [property: FromQuery("version")] int ExpectedVersion) : ITaskCommand, IValueId<Guid>;

[GenerateSerializer]
public class RebuildTaskViewCommand : RebuildViewCommandBase, IGrainKeyResolver<TaskItem>, IRequest<(ICommandResult, TaskItem)>;
