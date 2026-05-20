# Code Styling Guidelines

## C# Code Style

### Nullable Reference Types
All C# projects must have nullable reference types enabled globally.

**In your .csproj file, add:**
```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

This enforces strict null safety across the entire project. Always annotate nullable types explicitly:
```csharp
string? nullableString = null;
string nonNullableString = "value";
```

**Rules:**
- Use `?` suffix for nullable reference types
- Avoid `#pragma` directives to suppress warnings
- Resolve all nullable-related warnings before committing
- When null values are intentional, document them with comments

---

### Argument Validation
Use `ArgumentNullException.ThrowIfNull()` for all method arguments that should never be null.

```csharp
public void ProcessUser(User user)
{
    ArgumentNullException.ThrowIfNull(user);
    // Method logic
}
```

**Rules:**
- Check all non-nullable arguments at method entry
- Place validation at the top of the method
- No null-coalescing or conditional checks for required arguments

---

### Architecture & Design Patterns

#### Clean Architecture
- Organize code into distinct layers: Domain, Application, Infrastructure, Presentation
- Use use case driven design
- Dependencies flow inward; outer layers depend on inner layers
- Keep business logic in the Domain layer, independent of frameworks

#### Dependency Injection
- Always use DI containers for service creation
- Register services in the DI container at application startup
- Avoid service locators and static dependencies
- Constructor injection is the preferred method

**Example:**
```csharp
public class OrderService
{
    private readonly IOrderRepository _repository;

    public OrderService(IOrderRepository repository)
    {
        _repository = ArgumentNullException.ThrowIfNull(repository);
    }
}
```

#### Clean Code
- Use meaningful, intent-revealing names for classes, methods, and variables
- Keep methods small and focused on a single responsibility
- Avoid deep nesting and complex conditionals
- Extract complex logic into separate methods or services
- Use SOLID principles (Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion)

---

### Code Comments & Readability
- **Prioritize readability:** Write code that is self-explanatory through clear naming and structure
- **Comment complex logic only:** Explain the "why" not the "what"; avoid obvious comments
- **Document non-obvious behavior:** Edge cases, workarounds, or performance considerations

**Good comment:**
```csharp
// Retry logic: API rate limits requests, so we back off exponentially
await RetryWithBackoffAsync(() => apiClient.FetchAsync());
```

**Avoid obvious comments:**
```csharp
// Bad: This comment adds no value
i++; // Increment i
```

---

### Testing

#### Test Coverage
- Maintain at least 80% code coverage
- Focus on meaningful tests, not characteristic/line-coverage-only tests
- Test business logic, edge cases, and error conditions
- Avoid testing trivial getters/setters unless they contain logic

#### Test Quality
- Each test should verify one behavior
- Use descriptive test names that explain what is being tested and the expected outcome
- Arrange-Act-Assert pattern:
  ```csharp
  [Fact]
  public void TransferFunds_WithSufficientBalance_TransfersAmount()
  {
      // Arrange
      var account = new Account(1000m);
      
      // Act
      account.Transfer(200m);
      
      // Assert
      Assert.Equal(800m, account.Balance);
  }
  ```
- Mock external dependencies; test behavior in isolation
- Avoid testing implementation details; test observable behavior

#### What Not to Test
- Trivial auto-properties
- Framework/library behavior
- Constructor assignments without logic
- Simple CRUD operations without business logic

#### Integration Tests with SpecFlow
For scenario-based integration tests, use **SpecFlow** (BDD framework for .NET).

**Why SpecFlow:**
- Industry standard for BDD in C#
- Uses Gherkin syntax (Given-When-Then) — human-readable scenarios
- Step definitions map directly to C# code
- Integrates with NUnit, xUnit, and MSTest
- Works seamlessly with DI containers
- Tests describe business behavior, not implementation

**Install:**
```bash
dotnet add package SpecFlow
dotnet add package SpecFlow.NUnit
```

**Example scenario (.feature file):**
```gherkin
Feature: Fund Transfer
  Scenario: Transfer funds between accounts
    Given I have an account with balance of 1000
    And another account with balance of 500
    When I transfer 200 to the other account
    Then my account balance should be 800
    And the other account balance should be 700
```

**Step definitions (C#):**
```csharp
[Given("I have an account with balance of (.*)")]
public void GivenAccountWithBalance(decimal amount)
{
    _account = new Account(amount);
}

[When("I transfer (.*) to the other account")]
public void WhenTransfer(decimal amount)
{
    _account.Transfer(_otherAccount, amount);
}

[Then("my account balance should be (.*)")]
public void ThenBalanceShouldBe(decimal expected)
{
    Assert.Equal(expected, _account.Balance);
}
```

**Rules:**
- Each scenario tests one complete business workflow
- Use Given-When-Then format consistently
- Step definitions should be reusable across scenarios
- Mock external dependencies; test behavior in isolation
- Integrate with DI for realistic testing

---

## React / JavaScript Code Style

### Required Tools
- **ESLint**: Code quality and style enforcement
- **Prettier**: Automatic code formatting
- **TypeScript**: Type checking (optional but recommended for beginners)

### ESLint Configuration
Use a strict preset for clean code. Install:
```bash
npm install --save-dev eslint eslint-config-airbnb-typescript
```

**.eslintrc.json:**
```json
{
  "extends": ["airbnb-typescript"],
  "parserOptions": {
    "project": "./tsconfig.json"
  },
  "rules": {
    "no-console": "warn",
    "no-unused-vars": "error",
    "prefer-const": "error",
    "eqeqeq": "error"
  }
}
```

### Prettier Configuration
**.prettierrc:**
```json
{
  "semi": true,
  "singleQuote": false,
  "tabWidth": 2,
  "trailingComma": "es5",
  "printWidth": 100
}
```

**.prettierignore:**
```
node_modules
dist
build
.next
coverage
```

### npm Scripts
Add to **package.json**:
```json
{
  "scripts": {
    "lint": "eslint src/",
    "lint:fix": "eslint src/ --fix",
    "format": "prettier --write src/"
  }
}
```

**Rules:**
- Always fix linting errors before committing (`npm run lint:fix`)
- Run prettier on all files (`npm run format`)
- Use const, not let or var
- No console.log in production code (warnings only)
- Use double quotes for strings
- Strict equality checks (===, !==)
- No unused variables
- Max line length: 100 characters

---

## General Rules for Claude Agents

1. Always enable code quality tools before starting development
2. Run linters and formatters on all code before submission
3. Fix all errors and warnings
4. Follow the strict configurations above for clean, maintainable code
5. Document any exceptions or deviations in comments

---

## Build Verification

After **any** implementation — adding features, fixing bugs, refactoring, changing dependencies — the full solution build **must** pass cleanly before the work is considered done:

```bash
dotnet restore expense-tracker.sln
dotnet build expense-tracker.sln -c Release --no-restore
```

**Rules:**
- Zero errors and zero warnings are required — treat warnings as errors
- Run from the repo root (`/Users/jonathantrefz/sources/redesigned-potato`)
- If the build fails, fix it before committing; never commit a broken build
- When a `.csproj` is changed (package added/removed, framework changed), run `dotnet restore expense-tracker.sln` immediately
- If stale artifacts cause spurious copy errors, run `dotnet clean expense-tracker.sln` first

---

## Test Verification

After **any** implementation, **all tests must pass** before the work is considered done:

```bash
dotnet test expense-tracker.sln -c Release --no-build
```

**Rules:**
- All tests must pass — 0 failures, 0 errors
- Run from the repo root after a successful build
- Never commit code that causes test regressions
- If a test was previously passing and your change breaks it, fix the test or the code before committing — do not skip or delete tests to make the suite green
- The solution currently has **90 tests across 7 projects** — keep this count growing, never shrinking

---

## CI Pipeline Verification

After every push to `main`, verify the GitHub Actions pipeline passes:

1. Navigate to the **Actions** tab at `https://github.com/JTMoo/redesigned-potato/actions`
2. Confirm the latest **"Build and Test Services"** run is green
3. If any job is red, investigate and fix before continuing

**Rules:**
- Never leave a red pipeline unresolved
- The pipeline runs `dotnet restore`, `dotnet build`, and `dotnet test` for every service — it must mirror what passes locally
- When adding a new service, add it to the matrix in `.github/workflows/build-and-test.yml`

---

## Clarification Requirements

Claude agents **must** ask clarifying questions when:
- Requirements contain conflicting information
- Technical terms or definitions are unclear or missing
- Instructions are ambiguous or underspecified
- There are multiple valid interpretations of a request

Do not assume intent or proceed with guesses. Always verify understanding directly with the user before starting work.
