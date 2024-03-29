﻿using API.Data;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class MessagesController : BaseApiController
{
    private readonly IUserRepository _userRepository;
    private readonly IMessagesRepository _messagesRepository;
    private readonly IMapper _mapper;

    public MessagesController(IUserRepository userRepository, IMessagesRepository messagesRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _messagesRepository = messagesRepository;
        _mapper = mapper;
    }

    [HttpPost]
    public async Task<ActionResult<MessageDto>> CreateMessage(CreateMessageDto createMessageDto)
    {
        var username = User.GetUsername();

        if(username == createMessageDto.RecipientUsername?.ToLower()) return BadRequest("You cannot send messages to yourself");

        var sender = await _userRepository.GetUserByUsernameAsync(username);
        var recipient = await _userRepository.GetUserByUsernameAsync(createMessageDto.RecipientUsername);

        if(recipient == null) return NotFound();

        var message = new Message
        {
            Sender = sender,
            Recipient = recipient,
            SenderUsername = sender?.UserName,
            RecipientUsername = recipient.UserName,
            Content = createMessageDto.Content
        };

        _messagesRepository.AddMessage(message);

        if(await _messagesRepository.SaveAllAsync()) return Ok(_mapper.Map<MessageDto>(message));

        return BadRequest("Failed to send message");
    }

    [HttpGet]
    public async Task<ActionResult<PagedList<MessageDto>>> GetMessagesForUser([FromQuery] MessageParams messageParams)
    {
        messageParams.Username = User.GetUsername();

        var messages = await _messagesRepository.GetMessagesForUser(messageParams);

        Response.AddPaginationHeader(new PaginationHeader(messages.CurrentPage, messages.PageSize, messages.TotalCount, messages.TotalePages));

        return messages;
    }

    [HttpGet("thread/{username}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessageThread(string username)
    {
        var currentUsername = User.GetUsername();

        return Ok(await _messagesRepository.GetMessageThread(currentUsername!, username));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteMessage(int id)
    {
        var username = User.GetUsername();

        var message = await _messagesRepository.GetMessage(id);

        if(message?.SenderUsername != username && message?.RecipientUsername != username) return Unauthorized();

        if(message?.SenderUsername == username) message!.SenderDeleted = true;
        if(message?.RecipientUsername == username) message!.RecipientDeleted = true;

        if(message!.SenderDeleted && message.RecipientDeleted)
        {
            _messagesRepository.DeleteMessage(message);
        }

        if(await _messagesRepository.SaveAllAsync()) return Ok();

        return BadRequest("Problem deleting the message");
    }
}
