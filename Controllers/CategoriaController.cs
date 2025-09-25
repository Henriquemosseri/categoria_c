using Microsoft.AspNetCore.Mvc;
using Repository;

namespace performance_cache.Controllers
{
    public class CategoriaController : Controller
    {
        private readonly ICategoriaRepository categoriaRepository;
        public CategoriaController(ICategoriaRepository categoriaRepository)
        {
            //recebe a injeção de dependência da repository
            this.categoriaRepository = categoriaRepository;
        }
        public IActionResult Index()
        {
            return View();
        }
    }
}
