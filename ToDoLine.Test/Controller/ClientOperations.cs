﻿using IdentityModel.Client;
using Simple.OData.Client;
using System;
using System.Linq;
using System.Threading.Tasks;
using ToDoLine.Controller;
using ToDoLine.Dto;

namespace ToDoLine.Test.Controller
{
    public static class ClientOperations
    {
        /// <summary>
        /// Registers new user. If userName gets provided, it uses that for registration. Otherwise, it creates random user name.
        /// </summary>
        /// <returns>UserName. Useful when you've not provided fixed user name, so you can retrive generated user name's value.</returns>
        public static async Task<string> RegisterNewUser(this ToDoLineTestEnv testEnv, string userName = null)
        {
            userName = userName ?? Guid.NewGuid().ToString("N");

            IODataClient client = testEnv.Server.BuildODataClient(odataRouteName: "ToDoLine");

            await client.Controller<UserRegistrationController, UserRegistrationDto>()
                .Action(nameof(UserRegistrationController.Register))
                .Set(new UserRegistrationController.RegisterArgs { userRegistration = new UserRegistrationDto { UserName = userName, Password = "P@ssw0rd" } })
                .ExecuteAsync();

            return userName;
        }

        /// <summary>
        /// Login with provided user name. If registerNewUserByRandomUserName gets provided 'true' then it calls <see cref="RegisterNewUser(ToDoLineTestEnv, string)"/> first.
        /// You've to either provide a valid userName or pass registerNewUserByRandomUserName 'true'. It generates a random userName and returns that to you in case no userName is provided for registration.
        /// </summary>
        public static async Task<(string userName, IODataClient loggedInClient)> LoginInToApp(this ToDoLineTestEnv testEnv, bool registerNewUserByRandomUserName, string userName = null)
        {
            if (registerNewUserByRandomUserName == true)
            {
                userName = await RegisterNewUser(testEnv, userName);
            }
            else
            {
                if (userName == null)
                    throw new ArgumentNullException(nameof(userName));
            }

            TokenResponse token = await testEnv.Server.Login(userName, password: "P@ssw0rd", clientId: "ToDoLine", secret: "secret");

            IODataClient loggedInClient = testEnv.Server.BuildODataClient(odataRouteName: "ToDoLine", token: token);

            return (userName, loggedInClient);
        }

        public static async Task<UserDto> GetUserByUserName(this IODataClient client, string userName)
        {
            return (await client.Controller<UsersController, UserDto>()
                    .Function(nameof(UsersController.GetAllUsers))
                    .Filter(u => u.UserName.ToLower().Contains(userName.ToLower()))
                    .FindEntriesAsync()).Single();
        }
    }
}
