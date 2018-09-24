﻿using Bit.Core.Contracts;
using Bit.Data.Contracts;
using Bit.OData.ODataControllers;
using Bit.Owin.Exceptions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Converters;
using Swashbuckle.Examples;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ToDoLine.Dto;
using ToDoLine.Model;

namespace ToDoLine.Controller
{
    public class ToDoItemsController : DtoController<ToDoItemDto>
    {
        public virtual IRepository<ToDoItemOptions> ToDoItemOptionsListRepository { get; set; }
        public virtual IRepository<ToDoItem> ToDoItemsRepository { get; set; }
        public virtual IUserInformationProvider UserInformationProvider { get; set; }
        public virtual IDateTimeProvider DateTimeProvider { get; set; }

        [Function]
        public virtual IQueryable<ToDoItemDto> GetMyToDoItems()
        {
            Guid userId = Guid.Parse(UserInformationProvider.GetCurrentUserId());

            return ToDoItemOptionsListRepository.GetAll().Where(tdio => tdio.UserId == userId)
                  .Select(tdio => new ToDoItemDto
                  {
                      Id = tdio.ToDoItem.Id,
                      CreatedOn = tdio.ToDoItem.CreatedOn,
                      ModifiedOn = tdio.ToDoItem.ModifiedOn,
                      Title = tdio.ToDoItem.Title,
                      RemindOn = tdio.RemindOn,
                      CompletedBy = tdio.ToDoItem.CompletedBy.UserName,
                      CreatedBy = tdio.ToDoItem.CreatedBy.UserName,
                      DueDate = tdio.ToDoItem.DueDate,
                      IsCompleted = tdio.ToDoItem.IsCompleted,
                      IsImportant = tdio.ToDoItem.IsImportant,
                      Notes = tdio.ToDoItem.Notes,
                      ShowInMyDay = tdio.ShowInMyDayOn.Value.Date == DateTimeProvider.GetCurrentUtcDateTime().Date,
                      ToDoGroupId = tdio.ToDoItem.ToDoGroupId,
                      Steps = tdio.ToDoItem.Steps.Select(tdis => new ToDoItemStepDto
                      {
                          Id = tdis.Id,
                          IsCompleted = tdis.IsCompleted,
                          ToDoItemId = tdis.ToDoItemId,
                          Text = tdis.Text
                      }).ToList()
                  });
        }

        public class ToDoItemDtoCreateExamplesProvider : IExamplesProvider
        {
            public object GetExamples()
            {
                return new { Notes = "Sample note...", Title = "Title", ToDoGroupId = Guid.Empty, ShowInMyDay = true };
            }
        }

        [Create]
        [SwaggerRequestExample(typeof(ToDoItemDto), typeof(ToDoItemDtoCreateExamplesProvider), jsonConverter: typeof(StringEnumConverter))]
        public virtual async Task<ToDoItemDto> CreateToDoItem(ToDoItemDto toDoItem, CancellationToken cancellationToken)
        {
            if (toDoItem == null)
                throw new BadRequestException("ToDoItemMayNotBeNull");

            Guid userId = Guid.Parse(UserInformationProvider.GetCurrentUserId());

            ToDoItem addedToDoItem = await ToDoItemsRepository.AddAsync(new ToDoItem
            {
                Id = Guid.NewGuid(),
                CreatedById = userId,
                CreatedOn = DateTimeProvider.GetCurrentUtcDateTime(),
                ModifiedOn = DateTimeProvider.GetCurrentUtcDateTime(),
                Notes = toDoItem.Notes,
                Title = toDoItem.Title,
                ToDoGroupId = toDoItem.ToDoGroupId,
                Options = new List<ToDoItemOptions>
                {
                    new ToDoItemOptions
                    {
                        Id = Guid.NewGuid(),
                        ShowInMyDayOn = toDoItem.ShowInMyDay == true ? (DateTimeOffset?)DateTimeProvider.GetCurrentUtcDateTime() : null,
                        UserId = userId
                    }
                }
            }, cancellationToken);

            return await GetMyToDoItems()
                .FirstAsync(tdi => tdi.Id == addedToDoItem.Id, cancellationToken);
        }

        public class UpdateToDoItemExamplesProvider : IExamplesProvider
        {
            public object GetExamples()
            {
                return new
                {
                    Title = "Test",
                    IsImportant = true,
                    Notes = "Test",
                    DueDate = DateTimeOffset.UtcNow.AddDays(2),
                    IsCompleted = false,
                    RemindOn = DateTimeOffset.UtcNow.AddDays(1),
                    ShowInMyDay = false
                };
            }
        }

        [PartialUpdate]
        [SwaggerRequestExample(typeof(ToDoItemDto), typeof(UpdateToDoItemExamplesProvider), jsonConverter: typeof(StringEnumConverter))]
        public virtual async Task<ToDoItemDto> UpdateToDoItem(Guid key, ToDoItemDto toDoItem, CancellationToken cancellationToken)
        {
            if (toDoItem == null)
                throw new BadRequestException("ToDoItemMayNotBeNull");

            Guid userId = Guid.Parse(UserInformationProvider.GetCurrentUserId());

            ToDoItem toDoItemToBeModified = await ToDoItemsRepository.GetByIdAsync(cancellationToken, key);

            if (toDoItemToBeModified == null)
                throw new ResourceNotFoundException("ToDoItemCouldNotBeFound");

            ToDoItemOptions toDoItemOptionsToBeModified = await ToDoItemOptionsListRepository.GetAll()
                .FirstAsync(tdio => tdio.UserId == userId && tdio.ToDoItemId == key, cancellationToken);

            if (toDoItemOptionsToBeModified == null)
                throw new ResourceNotFoundException("ToDoItemCouldNotBeFound");

            toDoItemToBeModified.Title = toDoItem.Title;
            toDoItemToBeModified.IsImportant = toDoItem.IsImportant;
            toDoItemToBeModified.Notes = toDoItem.Notes;
            toDoItemToBeModified.DueDate = toDoItem.DueDate;

            toDoItemToBeModified.ModifiedOn = DateTimeProvider.GetCurrentUtcDateTime();

            if (toDoItemToBeModified.IsCompleted == false && toDoItem.IsCompleted == true)
            {
                toDoItemToBeModified.IsCompleted = true;
                toDoItemToBeModified.CompletedById = userId;
            }
            else if (toDoItemToBeModified.IsCompleted == true && toDoItem.IsCompleted == false)
            {
                toDoItemToBeModified.IsCompleted = false;
                toDoItemToBeModified.CompletedById = null;
            }

            toDoItemOptionsToBeModified.RemindOn = toDoItem.RemindOn;
            toDoItemOptionsToBeModified.ShowInMyDayOn = toDoItem.ShowInMyDay == true ? (DateTimeOffset?)DateTimeProvider.GetCurrentUtcDateTime() : null;

            await ToDoItemsRepository.UpdateAsync(toDoItemToBeModified, cancellationToken);
            await ToDoItemOptionsListRepository.UpdateAsync(toDoItemOptionsToBeModified, cancellationToken);

            return await GetMyToDoItems()
                .FirstAsync(tdi => tdi.Id == key, cancellationToken);
        }

        [Delete]
        public virtual async Task DeleteToDoItem(Guid key, CancellationToken cancellationToken)
        {
            Guid userId = Guid.Parse(UserInformationProvider.GetCurrentUserId());

            ToDoItem toDoItemToBeDeleted = await ToDoItemsRepository.GetByIdAsync(cancellationToken, key);

            if (toDoItemToBeDeleted == null)
                throw new ResourceNotFoundException("ToDoItemCouldNotBeFound");

            await ToDoItemsRepository.DeleteAsync(toDoItemToBeDeleted, cancellationToken);
        }
    }
}
