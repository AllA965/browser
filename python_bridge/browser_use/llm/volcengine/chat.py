from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any, TypeVar, overload

import httpx
from openai import (
	APIConnectionError,
	APIError,
	APIStatusError,
	APITimeoutError,
	AsyncOpenAI,
	RateLimitError,
)
from pydantic import BaseModel

from browser_use.llm.base import BaseChatModel
from browser_use.llm.volcengine.serializer import VolcengineMessageSerializer
from browser_use.llm.exceptions import ModelProviderError, ModelRateLimitError
from browser_use.llm.messages import BaseMessage, SystemMessage, ContentPartTextParam
from browser_use.llm.schema import SchemaOptimizer
from browser_use.llm.views import ChatInvokeCompletion

T = TypeVar('T', bound=BaseModel)


@dataclass
class ChatVolcengine(BaseChatModel):
	"""Volcengine Ark /chat/completions wrapper (OpenAI-compatible)."""

	model: str

	# Generation parameters
	max_tokens: int | None = None
	temperature: float | None = None
	top_p: float | None = None
	seed: int | None = None
	extra_body: dict[str, Any] | None = None

	# Connection parameters
	api_key: str | None = None
	base_url: str | httpx.URL | None = 'https://ark.cn-beijing.volces.com/api/v3'
	timeout: float | httpx.Timeout | None = None
	client_params: dict[str, Any] | None = None

	# Configuration parameters
	dont_force_structured_output: bool = False
	add_schema_to_system_prompt: bool = True

	@property
	def provider(self) -> str:
		return 'volcengine'

	def _client(self) -> AsyncOpenAI:
		return AsyncOpenAI(
			api_key=self.api_key,
			base_url=self.base_url,
			timeout=self.timeout,
			**(self.client_params or {}),
		)

	@property
	def name(self) -> str:
		return self.model

	@overload
	async def ainvoke(
		self,
		messages: list[BaseMessage],
		output_format: None = None,
		tools: list[dict[str, Any]] | None = None,
		stop: list[str] | None = None,
		**kwargs: Any,
	) -> ChatInvokeCompletion[str]: ...

	@overload
	async def ainvoke(
		self,
		messages: list[BaseMessage],
		output_format: type[T],
		tools: list[dict[str, Any]] | None = None,
		stop: list[str] | None = None,
		**kwargs: Any,
	) -> ChatInvokeCompletion[T]: ...

	async def ainvoke(
		self,
		messages: list[BaseMessage],
		output_format: type[T] | None = None,
		tools: list[dict[str, Any]] | None = None,
		stop: list[str] | None = None,
		**kwargs: Any,
	) -> ChatInvokeCompletion[T] | ChatInvokeCompletion[str]:
		"""
		Volcengine ainvoke supports:
		1. Regular text/multi-turn conversation
		2. Function Calling
		3. JSON Output (response_format)
		"""
		# Make a copy of messages to avoid modifying the original list/messages across retries
		copied_messages = [m.model_copy() for m in messages]
		
		client = self._client()
		
		# We need to serialize messages AFTER potential system prompt modification
		common: dict[str, Any] = {}

		if self.max_tokens is not None:
			common['max_tokens'] = self.max_tokens
		if self.temperature is not None:
			common['temperature'] = self.temperature
		if self.top_p is not None:
			common['top_p'] = self.top_p
		if self.seed is not None:
			common['seed'] = self.seed
		if self.extra_body is not None:
			common['extra_body'] = self.extra_body

		# Clean up kwargs: remove session_id as Volcengine/OpenAI create() doesn't support it
		kwargs.pop('session_id', None)

		try:
			if output_format:
				# Structured Output (using response_format)
				# Volcengine supports response_format for JSON output
				response_format = {
					'type': 'json_schema',
					'json_schema': {
						'name': output_format.__name__,
						'schema': SchemaOptimizer.create_optimized_json_schema(output_format),
						'strict': True,
					},
				}

				if self.add_schema_to_system_prompt and copied_messages and isinstance(copied_messages[0], SystemMessage):
					schema_text = f'\n<json_schema>\n{json.dumps(response_format["json_schema"]["schema"], indent=2)}\n</json_schema>'
					if isinstance(copied_messages[0].content, str):
						copied_messages[0].content += schema_text
					elif isinstance(copied_messages[0].content, list):
						copied_messages[0].content.append(ContentPartTextParam(text=schema_text))

				if not self.dont_force_structured_output:
					common['response_format'] = response_format

			volc_messages = VolcengineMessageSerializer.serialize_messages(copied_messages)

			if tools:
				common['tools'] = tools
				common['tool_choice'] = 'auto'

			response = await client.chat.completions.create(
				model=self.model,
				messages=volc_messages,
				stream=False,
				**common,
				**kwargs,
			)

			choice = response.choices[0]
			msg = choice.message

			if output_format:
				# Parse structured output
				try:
					data = json.loads(msg.content)
					parsed = output_format.model_validate(data)
					return ChatInvokeCompletion(completion=parsed, usage=None)
				except (json.JSONDecodeError, Exception) as e:
					raise ModelProviderError(f'Failed to parse structured output: {str(e)}') from e

			if msg.tool_calls:
				# Return the tool calls directly if the library supports it in ChatInvokeCompletion
				# Note: ToolCall might be available in newer versions of browser-use
				return ChatInvokeCompletion(completion=msg.content or '', usage=None, tool_calls=msg.tool_calls)

			return ChatInvokeCompletion(completion=msg.content or '', usage=None)

		except RateLimitError as e:
			raise ModelRateLimitError(str(e)) from e
		except (APIConnectionError, APITimeoutError, APIStatusError, APIError) as e:
			# Fallback: If json_schema is not supported, try again without forcing it
			if (
				isinstance(e, APIStatusError)
				and e.status_code == 400
				and 'json_schema' in str(e)
				and 'not supported' in str(e)
				and not self.dont_force_structured_output
			):
				# Retry once without forcing structured output
				original_dont_force = self.dont_force_structured_output
				self.dont_force_structured_output = True
				try:
					return await self.ainvoke(messages, output_format, tools, stop, **kwargs)
				finally:
					self.dont_force_structured_output = original_dont_force

			raise ModelProviderError(str(e)) from e
		except Exception as e:
			raise ModelProviderError(f'Unexpected error: {str(e)}') from e
