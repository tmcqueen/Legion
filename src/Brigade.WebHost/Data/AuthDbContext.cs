using Brigade.WebHost.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Brigade.WebHost.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) 
    : IdentityDbContext<ApplicationUser>(options)   
{
    
}