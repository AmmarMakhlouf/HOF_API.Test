using HOF_API.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace HOF_API.Test
{
    public class MainTest
    {
        string _connString = "server=localhost\\sqlexpress;database=halloffame_db;trusted_connection=true;";

        [Fact]
        public async Task TestGetPersons()
        {
            await using var application = new WebApplicationFactory<Program>();
            using var client = application.CreateClient();
            string response = await client.GetStringAsync("/v1/persons");
            int perId;
            string perName, perDisplayName;
            try
            {
                //sql connection object
                using (SqlConnection conn = new SqlConnection(_connString))
                {

                    //Fetch all Persons from database
                    string query = @"SELECT  Id,Name,DisplayName FROM Persons";
                    //define the SqlCommand object
                    SqlCommand cmd = new SqlCommand(query, conn);
                    //open connection
                    conn.Open();
                    //execute the SQLCommand
                    SqlDataReader dr = cmd.ExecuteReader();
                    ArrayList persons = new ArrayList();
                    //check if there are records
                    if (dr.HasRows)
                    {
                        while (dr.Read())
                        {
                            perId = dr.GetInt32(0);
                            perName = dr.GetString(1);
                            perDisplayName = dr.GetString(2);
                            Person person = new Person() { Id = perId, Name = perName, DisplayName = perDisplayName };
                            persons.Add(person);
                        }
                    }
                    JToken expected_response = JToken.Parse(JsonConvert.SerializeObject(persons));
                    //close data reader
                    dr.Close();
                    //close connection
                    conn.Close();

                    //Reading API Persons
                    JToken result_response = JToken.Parse((string)response);
                    Person[] received_persons = result_response.ToObject<Person[]>();
                    foreach (Person person in persons)
                    {
                        bool existed = false;
                        foreach(Person person1 in received_persons)
                            if(person1.Id == person.Id && person1.Name == person.Name
                                && person1.DisplayName == person.DisplayName)
                                existed = true;
                        Assert.True(existed,
                            "Person " + person.Id + " wasn't received!");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Some error happened while testing Persons API controller. Please re-run the test.");
                //display error message
                Trace.WriteLine("Exception: " + ex.Message);
                Assert.True(false, "Exception: " + ex.Message);
            }
        }

        [Fact]
        public async Task TestGetPerson()
        {
            await using var application = new WebApplicationFactory<Program>();
            using var client = application.CreateClient();
            string response = "";
            int perId = 0;
            string perName = "", perDisplayName = "";
            try
            {
                //sql connection object
                using (SqlConnection conn = new SqlConnection(_connString))
                {

                    //Read random row from database
                    string query = @"SELECT TOP 1 * FROM Persons ORDER BY newid()";
                    //define the SqlCommand object
                    SqlCommand cmd = new SqlCommand(query, conn);
                    //open connection
                    conn.Open();
                    //execute the SQLCommand
                    SqlDataReader dr = cmd.ExecuteReader();
                    //check if there are records
                    if (dr.HasRows)
                    {
                        if (dr.Read())
                        {
                            perId = dr.GetInt32(0);
                            perName = dr.GetString(1);
                            perDisplayName = dr.GetString(2);
                        }
                    }
                    response = await client.GetStringAsync(String.Concat("/v1/person/", perId));
                    //close data reader
                    dr.Close();
                    //close connection
                    conn.Close();

                    //Reading API Persons
                    Person resultData = JsonConvert.DeserializeObject<Person>(response);
                    string respons_name = resultData.Name;
                    string respons_disname = resultData.DisplayName;
                    Assert.Equal(perName, respons_name);
                    Assert.Equal(perDisplayName, respons_disname);
                    //Check sending id = null to API
                    var responseNull = await client.GetAsync(string.Concat("/v1/person/", 0));
                    Assert.Equal(HttpStatusCode.BadRequest,
                                  responseNull.StatusCode);//The resonse has to be 400 - Bad Request
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Some error happened while testing Persons API controller. Please re-run the test.");
                //display error message
                Trace.WriteLine("Exception: " + ex.Message);
                Assert.True(false, "Exception: " + ex.Message);
            }
        }

        [Fact]
        public async Task TestPostPerson()
        {
            await using var application = new WebApplicationFactory<Program>();
            using var client = application.CreateClient();
            string perName = "Testing Add", perDisplayName = RandomString(30);
            try
            {
                //Prepare a PersonData object
                PersonData personData = new PersonData()
                {
                    Person = new Person
                    {
                        Id = 0,
                        Name = perName,
                        DisplayName = perDisplayName
                    },
                    perSkills = new List<PerSkill> 
                    {
                                              new PerSkill
                                              { SkillId = 0,
                                                SkillName = "C#",
                                                SkillLevel = 10
                                              },
                                              new PerSkill
                                              {
                                                SkillId = 0,
                                                SkillName = "PHP",
                                                SkillLevel = 7
                                              }
                     },
                };
                var jsonRequest = JsonConvert.SerializeObject(personData);
                var buffer = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
                var byteContent = new ByteArrayContent(buffer);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await client.PostAsync("/v1/person/", byteContent);
                var content = await response.Content.ReadAsStringAsync();

                //Reading API Response
                PersonData resultData = JsonConvert.DeserializeObject<PersonData>(content);
                string respons_name = resultData.Person.Name;
                string respons_disname = resultData.Person.DisplayName;

                foreach (PerSkill perSkill in resultData.perSkills)
                {
                    if (perSkill != null)
                    {
                        if(perSkill.SkillName == "C#")
                            Assert.Equal((byte)10, perSkill.SkillLevel);
                        if(perSkill.SkillName == "PHP")
                            Assert.Equal((byte)7 , perSkill.SkillLevel);
                    }
                }
                Assert.Equal(perName, respons_name);
                Assert.Equal(perDisplayName, respons_disname);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Some error happened while testing Persons API controller. Please re-run the test.");
                //display error message
                Trace.WriteLine("Exception: " + ex.Message);
                Assert.True(false, "Exception: " + ex.Message);
            }
        }

        [Fact]
        public async Task TestPutPerson()
        {
            await using var application = new WebApplicationFactory<Program>();
            using var client = application.CreateClient();
            int perId = 0;
            string perName = "Testing Update", perDisplayName = RandomString(30);
            try
            {
                using (SqlConnection conn = new SqlConnection(_connString))
                {
                    //Read random person from database
                    string query = @"SELECT TOP 1 * FROM Persons ORDER BY newid()";
                    //define the SqlCommand object
                    SqlCommand cmd = new SqlCommand(query, conn);
                    //open connection
                    conn.Open();
                    //execute the SQLCommand
                    SqlDataReader dr = cmd.ExecuteReader();
                    //check if there are records
                    if (dr.HasRows)
                    {
                        if (dr.Read())
                        {
                            perId = dr.GetInt32(0);
                            perName = dr.GetString(1);
                            perDisplayName = dr.GetString(2);
                        }
                    }
                    //close data reader
                    dr.Close();
                    //close connection
                    conn.Close();
                    //Prepare a PersonData object
                    PersonData personData = new PersonData()
                    {
                        Person = new Person
                        {
                            Id = 0,
                            Name = perName,
                            DisplayName = perDisplayName
                        },
                        perSkills = new List<PerSkill> {
                                              new PerSkill
                                              { SkillId = 0,
                                                SkillName = "C#",
                                                SkillLevel = 9
                                              },
                                              new PerSkill
                                              {
                                                SkillId = 0,
                                                SkillName = "PHP",
                                                SkillLevel = 5
                                              }
                                    },

                    };
                    var jsonRequest = JsonConvert.SerializeObject(personData);
                    var buffer = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
                    var byteContent = new ByteArrayContent(buffer);
                    byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    var response = await client.PutAsync("/v1/person/"+perId, byteContent);
                    var content = await response.Content.ReadAsStringAsync();

                    //Reading API Response
                    PersonData resultData = JsonConvert.DeserializeObject<PersonData>(content);

                    string respons_name = resultData.Person.Name;
                    string respons_disname = resultData.Person.DisplayName;

                    foreach (PerSkill perSkill in resultData.perSkills)
                    {
                        if (perSkill != null)
                        {
                            if (perSkill.SkillName == "C#")
                                Assert.Equal(9, perSkill.SkillLevel);
                            if (perSkill.SkillName == "PHP")
                                Assert.Equal(5, perSkill.SkillLevel);
                        }
                    }

                    Assert.Equal(perName, respons_name);
                    Assert.Equal(perDisplayName, respons_disname);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Some error happened while testing Persons API controller. Please re-run the test.");
                //display error message
                Trace.WriteLine("Exception: " + ex.Message);
                Assert.True(false, "Exception: " + ex.Message);
            }
        }

        [Fact]
        public async Task TestDeletePerson()
        {
            await using var application = new WebApplicationFactory<Program>();
            using var client = application.CreateClient();
            int perId = 0;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connString))
                {
                    //Read random person from database
                    string query = @"SELECT TOP 1 * FROM Persons ORDER BY newid()";
                    //define the SqlCommand object
                    SqlCommand cmd = new SqlCommand(query, conn);
                    //open connection
                    conn.Open();
                    //execute the SQLCommand
                    SqlDataReader dr = cmd.ExecuteReader();
                    //check if there are records
                    if (dr.HasRows)
                    {
                        if (dr.Read())
                        {
                            perId = dr.GetInt32(0);
                        }
                    }
                    //close data reader
                    dr.Close();
                    //close connection
                    conn.Close();
                    //Send Delete to API
                    var response = await client.DeleteAsync("/v1/person/" + perId);
                    var content = await response.Content.ReadAsStringAsync();

                    //Reading again from database
                    query = @"SELECT count(*) FROM Persons where Id = "+perId;
                    cmd = new SqlCommand(query, conn);
                    //open connection
                    conn.Open();
                    //execute the SQLCommand
                    SqlDataReader dr1 = cmd.ExecuteReader();
                    //check if there are records
                    if (dr1.HasRows)
                    {
                        if (dr1.Read())
                        {
                            int count = dr1.GetInt32(0);
                            if (count > 0)
                                Assert.True(false);
                        }
                    }
                    //close data reader
                    dr1.Close();
                    //close connection
                    conn.Close();

                    //Checking that the PersonSkills Table has no more any
                    //record related to the deleted Person
                    query = @"SELECT count(*) FROM PersonSkills where PersonId = " + perId;
                    cmd = new SqlCommand(query, conn);
                    //open connection
                    conn.Open();
                    //execute the SQLCommand
                    SqlDataReader dr2 = cmd.ExecuteReader();
                    //check if there are records
                    if (dr2.HasRows)
                    {
                        if (dr2.Read())
                        {
                            int count = dr2.GetInt32(0);
                            if (count > 0)
                                Assert.True(false);
                        }
                    }
                    //close data reader
                    dr2.Close();
                    //close connection
                    conn.Close();
                    Assert.True(true);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Some error happened while testing Persons API controller. Please re-run the test.");
                //display error message
                Trace.WriteLine("Exception: " + ex.Message);
                Assert.True(false, "Exception: " + ex.Message);
            }
        }
        private static Random _random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());

        }
    }
}
