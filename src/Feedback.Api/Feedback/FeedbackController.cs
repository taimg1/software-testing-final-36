using Feedback.Api.Domain;
using Feedback.Api.Feedback.Requests;
using Feedback.Api.Feedback.Responses;
using Feedback.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Feedback.Api.Feedback;

[ApiController]
[Route("api/feedback")]
public class FeedbackController(
    IFeedbackService feedbackService,
    IValidator<CreateFeedbackRequest> createValidator,
    IValidator<UpdateFeedbackRequest> updateValidator,
    IValidator<AddVoteRequest> voteValidator,
    IValidator<AddCommentRequest> commentValidator) : ControllerBase
{
    [HttpGet]
    public async Task<Ok<IReadOnlyList<FeedbackResponse>>> GetAll(
        [FromQuery] FeedbackType? type,
        [FromQuery] FeedbackStatus? status,
        [FromQuery] bool sortByVotes = false)
    {
        var result = await feedbackService.GetAllAsync(type, status, sortByVotes);
        return TypedResults.Ok(result);
    }

    [HttpGet("stats")]
    public async Task<Ok<FeedbackStatsResponse>> GetStats()
    {
        var result = await feedbackService.GetStatsAsync();
        return TypedResults.Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<Results<Ok<FeedbackDetailResponse>, NotFound>> GetById(int id)
    {
        try
        {
            var result = await feedbackService.GetByIdAsync(id);
            return TypedResults.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    [HttpPost]
    public async Task<Results<Created<FeedbackResponse>, ValidationProblem>> Create(
        [FromBody] CreateFeedbackRequest request)
    {
        var validation = await createValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return TypedResults.ValidationProblem(validation.ToDictionary());

        var result = await feedbackService.CreateAsync(request);
        return TypedResults.Created($"/api/feedback/{result.Id}", result);
    }

    [HttpPut("{id:int}")]
    public async Task<Results<Ok<FeedbackResponse>, NotFound, ValidationProblem>> Update(
        int id, [FromBody] UpdateFeedbackRequest request)
    {
        var validation = await updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return TypedResults.ValidationProblem(validation.ToDictionary());

        try
        {
            var result = await feedbackService.UpdateAsync(id, request);
            return TypedResults.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<Results<Ok<FeedbackResponse>, NotFound, Conflict<string>>> UpdateStatus(
        int id, [FromBody] UpdateFeedbackStatusRequest request)
    {
        try
        {
            var result = await feedbackService.UpdateStatusAsync(id, request.Status);
            return TypedResults.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(ex.Message);
        }
    }

    [HttpPost("{id:int}/vote")]
    public async Task<Results<Created<VoteResponse>, NotFound, Conflict<string>, ValidationProblem>> AddVote(
        int id, [FromBody] AddVoteRequest request)
    {
        var validation = await voteValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return TypedResults.ValidationProblem(validation.ToDictionary());

        try
        {
            var result = await feedbackService.AddVoteAsync(id, request);
            return TypedResults.Created($"/api/feedback/{id}/vote", result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(ex.Message);
        }
    }

    [HttpDelete("{id:int}/vote")]
    public async Task<Results<NoContent, NotFound>> RemoveVote(
        int id, [FromQuery] string voterEmail)
    {
        try
        {
            await feedbackService.RemoveVoteAsync(id, voterEmail);
            return TypedResults.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    [HttpPost("{id:int}/comments")]
    public async Task<Results<Created<CommentResponse>, NotFound, ValidationProblem>> AddComment(
        int id, [FromBody] AddCommentRequest request)
    {
        var validation = await commentValidator.ValidateAsync(request);
        if (!validation.IsValid)
            return TypedResults.ValidationProblem(validation.ToDictionary());

        try
        {
            var result = await feedbackService.AddCommentAsync(id, request);
            return TypedResults.Created($"/api/feedback/{id}/comments/{result.Id}", result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }
}
